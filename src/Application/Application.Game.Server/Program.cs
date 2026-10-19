
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Game.Server.Entities.Configurations;
using Application.Game.Server.Hubs;
using Application.Game.Server.Services;
using Application.Game.Server.Workers;
using Domain.Game.Extensions;
using Domain.Game.Rules;
using Library.Commons.Api.Contants;
using Library.Commons.Api.Entities;
using Library.Commons.Api.Extensions;
using Library.Commons.Api.Interfaces;
using Library.Commons.Eventbus.RabbitMq.Builders;
using Library.Commons.Game.Domain.Constants;
using Library.Commons.Game.Domain.Entities.Configurations;
using Library.Commons.Game.Domain.Entities.Eventbus.Payloads;
using Library.Commons.Game.Domain.Interfaces.Services;
using Library.Commons.Game.Server.Constants;
using Library.Commons.Game.Server.Extensions;
using Library.Extensions.DependencyInjection.Extensions;

namespace Application.Game.Server
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Host.ConfigureLogger(builder.Logging);
            builder.Configuration.InitializeAppConfiguration(builder.Configuration.GetCurrentDirectoryBasePath("Configurations"));
            var appSettings = builder.Services.AddAppSettings<AppSettings>(builder.Configuration);
            var corsPolicyName = builder.Services.AddAllowAllCorsPolicy();
            builder.ConfigureDepencyInjection(appSettings, ConfigureServices);
            var app = builder.Build();
            app.Configure(appSettings, corsPolicyName, Configure);
            await app.RunAsync();
        }
        private static IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration, AppSettings appSettings)
        {

            var jsonSerializerOptions = BuilderJsonSerializerOptions();
            AddDefaultServerServices(services, appSettings,jsonSerializerOptions);
            AddGameServices(services, configuration);
            ConfigureEventbus(services, configuration, appSettings);
            AddJsonSerializerOptions(services);

            services.AddManagementApi(appSettings.ManagementApiConfiguration);

            return services;
        }

        private static JsonSerializerOptions BuilderJsonSerializerOptions()
        {
            return GameJsonOptions.Create();
        }

        private static void AddJsonSerializerOptions(IServiceCollection services, JsonSerializerOptions? jsonSerializerOptions=null)
        {
            services.AddJsonSerializeOptions(() => jsonSerializerOptions??BuilderJsonSerializerOptions());
        }

        private static void AddDefaultServerServices(IServiceCollection services, AppSettings appSettings,
            JsonSerializerOptions? jsonSerializerOptions=null)
        {
            services.AddSignalR(c => { c.EnableDetailedErrors = true;
                })
                .AddJsonProtocol(c => c.PayloadSerializerOptions = jsonSerializerOptions ?? BuilderJsonSerializerOptions());
            services.AddHealthChecks();
            services.AddRequestExceptionHandler<IRequestExceptionHandler, Handlers.ExceptionHandler>();
            services.AddCorsPolicy(appSettings.CorsConfiguration);
        }

        private static void AddGameServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddGameServices(
                perkOptions: configuration.GetSection(PerkOptions.SectionName).Get<PerkOptions>(),
                prizeOptions: configuration.GetSection(PrizeOptions.SectionName).Get<PrizeOptions>(),
                worldEventOptions: configuration.GetSection(WorldEventOptions.SectionName).Get<WorldEventOptions>());
            services.AddGameState();
            services.AddRooms();
            services.AddPlayers();
            services.AddPlayerRooms();
            services.AddServerConfiguration(configuration);
            services.AddFoodConfiguration(configuration);
            services.AddSnakeConfiguration(configuration,$"{ServerConfiguration.SectionName}:{SnakeConfiguration.SectionName}");
            services.AddSingleton<GameplayApplier>(provider => new GameplayApplier(
                provider.GetRequiredService<PerkRules>(), provider.GetRequiredService<PrizeRules>(), provider.GetRequiredService<RoomLifetimeSettings>(),
                provider.GetRequiredService<WorldEventRules>(), provider.GetRequiredService<Serilog.ILogger>()));
            services.AddSingleton<IEventTriggers, EventTriggers>();
            services.AddSingleton<RoomGameplayService>();
            services.AddHostedService<GameplaySyncWorkerService>();
            services.AddHostedService<FoodGeneratorWorkerService>();
            services.AddSingleton<JoinRequestHandler>();
            services.AddSingleton<RoomCreateRequestHandler>();
            services.AddSingleton<IRoomClosedNotifier, HubRoomClosedNotifier>();
            services.AddSingleton<RoomCloser>();
            services.AddSingleton<IRoomCloser>(provider => provider.GetRequiredService<RoomCloser>());
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<EmptyRoomReaper>(provider => new EmptyRoomReaper(
                provider.GetRequiredService<IRoomsService>(), provider.GetRequiredService<IPlayerRoomService>(),
                provider.GetRequiredService<IRoomCloser>(), provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<RoomLifetimeSettings>()));
            services.AddHostedService<EmptyRoomReaperWorkerService>();
            services.AddHostedService<EventbusWorkerService>();
            services.AddHostedService<RoomHeartbeatWorkerService>();
            services.AddHostedService<GameWorkerService>();
        }

        private static void ConfigureEventbus(IServiceCollection services, IConfiguration configuration, AppSettings appSettings)
        {
            var builders = new RabbitMqBuilders(new Uri(appSettings.EventbusConfiguration.ConnectionString));
            services.Add(builders.BuildConsumer<PlayerJoinPayload>(builders.Connection).Configure(EventNames.PlayerJoinRequest, declareQueue: false));
            services.Add(builders.BuildConsumer<RoomCreatePayload>(builders.Connection).Configure(EventNames.RoomCreateRequest, declareQueue: false));
        }

        /// <summary>Called by the manager when an administrator deletes a room. Same API key the game server uses toward the manager.</summary>
        private static async Task<IResult> CloseRoomAsync(Guid roomId, HttpContext context, IConfiguration configuration, RoomCloser closer)
        {
            var expected = configuration["AppSettings:ManagementApiConfiguration:ApiKey"];
            var received = context.Request.Headers[AuthConstants.ApiKeyHeaderName].ToString();
            if (string.IsNullOrEmpty(expected) || received != expected) return Results.Unauthorized();
            return await closer.CloseAsync(roomId, cancellationToken: context.RequestAborted) ? Results.NoContent() : Results.NotFound();
        }

        private static bool IsManager(HttpContext context, IConfiguration configuration)
        {
            var expected = configuration["AppSettings:ManagementApiConfiguration:ApiKey"];
            return !string.IsNullOrEmpty(expected) && context.Request.Headers[AuthConstants.ApiKeyHeaderName].ToString() == expected;
        }

        /// <summary>Called by the manager when an administrator changes what a room does with the gameplay.</summary>
        private static IResult SetRoomGameplay(Guid roomId, Dictionary<string, string>? choices, HttpContext context, IConfiguration configuration, RoomGameplayService service)
        {
            if (!IsManager(context, configuration)) return Results.Unauthorized();
            return service.Set(roomId, Library.Commons.Game.Domain.Entities.RoomGameplay.FromMap(choices)) ? Results.NoContent() : Results.NotFound();
        }

        /// <summary>Called by the manager when an administrator starts a world event in a room.</summary>
        private static IResult TriggerRoomEvent(Guid roomId, string kind, HttpContext context, IConfiguration configuration, RoomGameplayService service)
        {
            if (!IsManager(context, configuration)) return Results.Unauthorized();
            return service.Trigger(roomId, kind) switch
            {
                TriggerResult.Started => Results.NoContent(),
                TriggerResult.NotNow => Results.Conflict(),
                TriggerResult.UnknownEvent => Results.BadRequest(),
                _ => Results.NotFound()
            };
        }

        private static void Configure(IApplicationBuilder app, IHostEnvironment env, AppSettings appSettings, CorsPolicyName corsPolicyName)
        {
            app.UseCors(corsPolicyName);
            app.UseHttpsRedirection();

            app.UseAuthorization();
            app.UseRouting();
            app.UseHealthChecks(HealthCheckDefaultValues.HealthCheckPath);
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<GameHub>(endpoints.CreateHubEndPoint(HubEndpoints.Game)).Finally(c =>
                {

                });
                endpoints.MapDelete("/rooms/{roomId:guid}", CloseRoomAsync);
                endpoints.MapPut("/rooms/{roomId:guid}/gameplay", SetRoomGameplay);
                endpoints.MapPost("/rooms/{roomId:guid}/events/{kind}", TriggerRoomEvent);

            });
        }
    }
}