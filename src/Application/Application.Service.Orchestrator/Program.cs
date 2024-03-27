using Application.Service.Orchestrator.Entities.Configurations;
using Application.Service.Orchestrator.Services;
using Docker.DotNet;
using Library.Extensions.DependencyInjection.Extensions;

namespace Application.Service.Orchestrator;

public  class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.InitializeAppConfiguration(
            builder.Configuration.GetCurrentDirectoryBasePath("Configurations"));
        var appSettings = builder.Services.AddAppSettings<AppSettings>(builder.Configuration);
        builder.Logging.ConfigureLogger(builder.Configuration);
        builder.Services.AddHostedService<WorkerService>();
        builder.Services.AddScoped<ContainerService>();
        builder.Services.AddScoped<IDockerClient>(serviceProvider =>
            new DockerClientConfiguration(new Uri(appSettings.DockerDeamonConfiguration.EndPoint)).CreateClient());
        var app = builder.Build();
        await app.RunAsync();
    }
}