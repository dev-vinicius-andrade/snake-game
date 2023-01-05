
using Application.Game.Server.Entities.Configurartions;
using Application.Game.Server.Hubs;
using Library.Commons.Api.Contants;
using Library.Commons.Api.Entities;
using Library.Commons.Api.Extensions;
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
            services.AddSignalR();
            services.AddHealthChecks();
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerDocumentation(appSettings.SwaggerConfiguration);
            services.AddDefaultRequestExceptionHandler();
            services.AddCorsPolicy(appSettings.CorsConfiguration);
            services.AddHostedService<GameServerWorker>();
            return services;
        }
        private static void Configure(IApplicationBuilder app, IHostEnvironment env, AppSettings appSettings, CorsPolicyName corsPolicyName)
        {
            app.UseSwaggerDocumentation(appSettings.SwaggerConfiguration);
            app.ConfigureExceptionHandling(env);
            app.UseCors(corsPolicyName);
            if (env.IsDevelopment())
                app.UseSwaggerDocumentation(appSettings.SwaggerConfiguration);

            app.UseHttpsRedirection();

            app.UseAuthorization();
            app.UseRouting();
            app.UseHealthChecks(HealthCheckDefaultValues.HealthCheckPath);
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<GameHub>(endpoints.CreateHubEndPoint(HubEndpoints.Game));
                endpoints.MapControllers();
            });
        }
    }
}