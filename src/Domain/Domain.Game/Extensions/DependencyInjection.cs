using Domain.Game.Constants;
using Domain.Game.Services;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Domain.Game.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddTrackableIdGenerator(this IServiceCollection services)
    {
        services.AddSingleton<ITrackableIdGeneratorService, TrackableIdGeneratorService>();
        return services;
    }
    public static IServiceCollection AddGameServices(this IServiceCollection services, IEnumerable<KeyValuePair<Direction, IPointAxis>>? directions = null)
    {
        if (directions is not null)
            services.AddKnowDirections(directions);
        else
            services.AddKnowDirections();
        services.AddSingleton<IPlayerService, PlayerService>();
        services.AddSingleton<IRoomsService, RoomsService>();
        services.AddSingleton<IGameStateService, GameStateService>();
        services.AddSingleton<IPlayerRoomService, PlayerRoomService>();
        services.AddSingleton<ITrackableIdGeneratorService, TrackableIdGeneratorService>();
        services.AddSingleton<ITimeSpanService,TimeSpanService>();
        services.AddSingleton<IPointAxisServices, PointAxisServices>();
        services.AddSingleton<ITickRateService, TickRateService>();
        services.AddSingleton<IObstacleService, ObstacleService>();
        return services;
    }
    public static IServiceCollection AddKnowDirections(this IServiceCollection services)
    {
        var knownDirections = new Dictionary<Direction, IPointAxis>(new List<KeyValuePair<Direction, IPointAxis>>
        {
            DefaultDirections.Up,
            DefaultDirections.Down,
            DefaultDirections.Left,
            DefaultDirections.Right
        });

        services.AddKnowDirections(knownDirections);
        return services;
    }
    public static IServiceCollection AddKnowDirections(this IServiceCollection services, IEnumerable<KeyValuePair<Direction, IPointAxis>> directions)
    {
        var knownDirections = new Dictionary<Direction, IPointAxis>(directions);
        services.AddSingleton<IReadOnlyDictionary<Direction, IPointAxis>>(knownDirections);
        services.AddSingleton<IKnowDirectionsService, KnowDirectionsService>();
        return services;
    }
}