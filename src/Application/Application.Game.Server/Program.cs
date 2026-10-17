
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Game.Server.Entities.Configurations;
using Application.Game.Server.Hubs;
using Application.Game.Server.Services;
using Application.Game.Server.Workers;
using Domain.Game.Extensions;
using Library.Commons.Api.Contants;
using Library.Commons.Api.Entities;
using Library.Commons.Api.Extensions;
using Library.Commons.Api.Interfaces;
using Library.Commons.Eventbus.RabbitMq.Builders;
using Library.Commons.Game.Domain.Constants;
using Library.Commons.Game.Domain.Entities.Configurations;
using Library.Commons.Game.Domain.Entities.Eventbus.Payloads;
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
            return new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };
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
            services.AddGameServices();
            services.AddGameState();
            services.AddRooms();
            services.AddPlayers();
            services.AddPlayerRooms();
            services.AddServerConfiguration(configuration);
            services.AddFoodConfiguration(configuration);
            services.AddSnakeConfiguration(configuration,$"{ServerConfiguration.SectionName}:{SnakeConfiguration.SectionName}");
            services.AddHostedService<FoodGeneratorWorkerService>();
            services.AddSingleton<JoinRequestHandler>();
            services.AddSingleton<RoomCreateRequestHandler>();
            services.AddSingleton<IRoomClosedNotifier, HubRoomClosedNotifier>();
            services.AddSingleton<RoomCloser>();
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

            });
        }
    }
}