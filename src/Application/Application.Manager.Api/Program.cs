
using Application.Manager.Api.Entities.Configurations;
using Application.Manager.Api.Hubs;
using Docker.DotNet;
using Library.Commons.Api.Contants;
using Library.Commons.Api.Entities;
using Library.Commons.Api.Extensions;
using Library.Commons.Eventbus.RabbitMq.Builders;
using Library.Commons.Game.Domain.Constants;
using Library.Commons.Game.Server.Constants;
using Library.Commons.Game.Server.Extensions;
using Library.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Json;
using Library.Commons.Eventbus.RabbitMq.Configurations;
using Library.Commons.Eventbus.RabbitMq.Enums;
using Library.Commons.Eventbus.RabbitMq.Interfaces;

namespace Application.Manager.Api
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
            ConfigureEventbus(services, configuration, appSettings);
            ConfigureRedis(services, appSettings);
            
            services.AddSignalR().AddJsonProtocol();
            services.AddHealthChecks();
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerDocumentation(appSettings.SwaggerConfiguration);
            services.AddDefaultRequestExceptionHandler();
            services.AddCorsPolicy(appSettings.CorsConfiguration);
            services.AddApiKeyAuth(configuration);
            

            services.AddJsonSerializeOptions(() => new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,

            });
            return services;
        }

        private static void ConfigureRedis(IServiceCollection services, AppSettings appSettings)
        {
            services.AddStackExchangeRedisCache(options =>
            {
                var redisSettings = appSettings.RedisConfiguration;
                options.ConfigurationOptions = new ConfigurationOptions
                {
                    AbortOnConnectFail = false,
                    ChannelPrefix = string.IsNullOrWhiteSpace(redisSettings.ChannelPrefix)
                        ? string.Empty
                        : redisSettings.ChannelPrefix,
                    Password = redisSettings.Password,
                    EndPoints = { { redisSettings.Host, redisSettings.Port ?? 6379 } }
                };
                options.ConnectionMultiplexerFactory = async () =>
                {
                    options.ConfigurationOptions.EndPoints.Add(redisSettings.Host, redisSettings.Port ?? 6379);
                    options.ConfigurationOptions.SetDefaultPorts();
                    var connection = await ConnectionMultiplexer.ConnectAsync(options.ConfigurationOptions);
                    connection.ConnectionRestored += (sender, args) =>
                        Console.WriteLine("Connection to Redis restored...");
                    connection.ConnectionFailed += (_, e) =>
                        Console.WriteLine("Connection to Redis failed...");

                    Console.WriteLine(!connection.IsConnected
                        ? $"Could not stablish connection with redis:{redisSettings.Host}:{redisSettings.Port}"
                        : $"Connection stablished with redis:{redisSettings.Host}:{redisSettings.Port}");

                    return connection;
                };
            });
        }
        private static void ConfigureEventbus(IServiceCollection services, IConfiguration configuration, AppSettings appSettings)
        {
            var builders = new RabbitMqBuilders(new Uri(appSettings.EventbusConfiguration.ConnectionString));
            var setups = new List<IEventbusSetup>
            {
                builders.BuidlEventbusSetup(new RabbitMqPublisherConfiguration
                {
                    Name = EventNames.PlayerJoinRequest,
                    CreateDefaultQueue = true,
                    Durable = true,
                    Type = ExchangeTypeEnum.Fanout,
                    QueuesConfiguration =
                    {
                        new RabbitMqReaderConfiguration()
                        {
                            Name = EventNames.PlayerJoinRequest,
                            Durable = true,
                            AutoDelete = false,
                            Exclusive = false
                        }
                    }

                })
            };
            foreach (var setup in setups)
                setup.Configure();

            services.Add(builders.BuildPublisher());
        }

        private static void Configure(IApplicationBuilder app, IHostEnvironment env, AppSettings appSettings, CorsPolicyName corsPolicyName)
        {
            app.UseSwaggerDocumentation(appSettings.SwaggerConfiguration);
            app.ConfigureExceptionHandling(env);
            app.UseCors(corsPolicyName);
            if (env.IsDevelopment())
                app.UseSwaggerDocumentation(appSettings.SwaggerConfiguration);

            app.UseHttpsRedirection();


            app.UseRouting();
            app.UseHealthChecks(HealthCheckDefaultValues.HealthCheckPath);
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<ManagerHub>(endpoints.CreateHubEndPoint(HubEndpoints.Manager));
            });
        }

    }
}
