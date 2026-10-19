namespace Application.Game.Server.Services;

public static class JoinTicketServiceCollectionExtensions
{
    /// <summary>Join tickets of signed-in players and the hub connections of their accounts (docs/ACHIEVEMENTS.md).</summary>
    public static IServiceCollection AddJoinTickets(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<JoinTickets>();
        services.AddSingleton<AccountConnections>();
        return services;
    }
}
