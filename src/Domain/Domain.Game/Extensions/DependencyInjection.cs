using Domain.Game.Constants;
using Domain.Game.Rules;
using Domain.Game.Services;
using Domain.Game.Simulation;
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
    public static IServiceCollection AddGameServices(this IServiceCollection services, IEnumerable<KeyValuePair<Direction, IPointAxis>>? directions = null,
        PerkOptions? perkOptions = null, PrizeOptions? prizeOptions = null)
    {
        if (directions is not null)
            services.AddKnowDirections(directions);
        else
            services.AddKnowDirections();
        services.AddSingleton<IPlayerService, PlayerService>();
        services.AddSingleton<IRoomsService, RoomsService>();
        services.AddSingleton<IRoomJoinRouter, RoomJoinRouter>();
        services.AddSingleton<IGameStateService, GameStateService>();
        services.AddSingleton<IPlayerRoomService, PlayerRoomService>();
        services.AddSingleton<ITrackableIdGeneratorService, TrackableIdGeneratorService>();
        services.AddSingleton<ITimeSpanService,TimeSpanService>();
        services.AddSingleton<IPointAxisServices, PointAxisServices>();
        services.AddSingleton<ITickRateService, TickRateService>();
        services.AddSingleton<IObstacleService, ObstacleService>();
        services.AddSingleton(new SnakeRulesOptions());
        services.AddSingleton(new DamageModelOptions());
        services.AddSingleton(perkOptions ?? new PerkOptions());
        services.AddSingleton(prizeOptions ?? new PrizeOptions());
        services.AddSingleton<PerkRules>();
        services.AddSingleton<PrizeRules>();
        services.AddSingleton<PrizeSpawner>();
        services.AddSingleton<DamageCalculator>();
        services.AddSingleton<BodyHealer>();
        services.AddSingleton<BodyHealthRules>();
        services.AddSingleton<SnakeBodyRules>();
        services.AddSingleton<BodyShotRules>();
        services.AddSingleton<BoostRules>();
        services.AddSingleton<ShotCooldownRules>();
        services.AddSingleton<HeadingRules>();
        services.AddSingleton<ShotRangeRules>();
        services.AddSingleton<SnakeSizeRules>();
        services.AddSingleton<SnakeCollisionRules>();
        services.AddSingleton<DeathDropRules>();
        services.AddSingleton<IRoomInputQueue, RoomInputQueue>();
        services.AddSingleton<RoomSimulation>();
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