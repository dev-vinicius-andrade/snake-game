using Application.Game.Server.Workers;
using Library.Commons.Api.Contants;
using Library.Commons.Entities.Configurations;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Application.Game.Server.Stats;

public static class StatsServiceCollectionExtensions
{
    /// <summary>
    /// The stats of the lives of signed-in players, sent to the manager (docs/ACHIEVEMENTS.md) with the same address and api key as the
    /// other calls of the game server toward it, and what they unlock told to the players. Needs <c>AddJoinTickets</c> and SignalR.
    /// </summary>
    public static IServiceCollection AddAccountStats(this IServiceCollection services, ApiIntegrationConfiguration manager,
        StatsReporterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(manager);
        services.AddHttpClient(ManagerAccountStatsClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(manager.BaseUrl);
            client.DefaultRequestHeaders.Add(AuthConstants.ApiKeyHeaderName, manager.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(10);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        });
        services.AddSingleton<IAccountStatsClient, ManagerAccountStatsClient>();
        services.AddSingleton<IAchievementsNotifier, HubAchievementsNotifier>();
        services.AddSingleton(options ?? new StatsReporterOptions());
        services.AddSingleton<HttpStatsReporter>();
        services.AddSingleton<IStatsReporter>(provider => provider.GetRequiredService<HttpStatsReporter>());
        services.AddHostedService<StatsReportWorkerService>();
        return services;
    }
}
