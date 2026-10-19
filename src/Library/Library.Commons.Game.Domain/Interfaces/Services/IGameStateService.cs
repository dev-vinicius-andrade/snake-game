using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Domain.Interfaces.Services;

public interface IGameStateService
{
    Task<IPlayer?> CreatePlayerInRoom(string playerId, string nickname, Guid roomId,
        CancellationToken cancellationToken=default);

    IList<IPointAxis> GenerateInitialPath(IPointAxis initialPosition, IPointAxis initialDirection);
    KeyValuePair<Direction, IPointAxis>? RandomDirection() ;

    IPointAxis GenerateInitialPosition(in int snakeConfigurationHeadSize,
        in int xMaxValue, in int yMaxValue);

    IColor GetAvailableColor(IRoom room);
    IPointAxis GetAvailablePosition(IRoom room);
    IFood GenerateFood(IRoom room);
    string GetRandomAvailableColor(IRoom room);
    bool IsColorBeeingUsed(IRoom room, string color);
    bool AnyCharWithColor(IRoom room, string color);
    bool AnyFoodWithColor(IRoom room, string color);
    bool HasPositionBeeingUsed(IRoom room, IPointAxis position, int delta = 0);
    bool AnyCharInPosition(IRoom room, IPointAxis position, int delta = 0);
    bool AnyFoodInPosition(IRoom room, IPointAxis position, int delta = 0);
    IGameState? GetGameState(ITrackableId roomId);
    IGameState CreateGameState(IRoom room);
    void RemoveGameState(ITrackableId roomId);
    bool ChangeDirectionByPlayerId(string playerConnectionId, Direction? direction = null);
    bool SetBoostByPlayerId(string playerConnectionId, bool active);
    bool SteerByPlayerId(string playerConnectionId, double angle);
    Task MovePlayer(IGameState gameState, IPlayer player);
    bool CanGenerateFood(IGameState gameState);
    Task<(IPlayer player,string oldPlayerConnectionId)?> ReconnectPlayer(ITrackableId roomTrackableId, ITrackableId playerTrackableId, string playerConnectionId);
    Task<bool> DisconnectPlayer(ITrackableId roomTrackableId, ITrackableId playerTrackableId);
    Task CheckFoodCollision(IGameState gameState, IPlayer player);
    Task<IList<IScore>> GetScore(IGameState gameState, int skip=0, int take=10);
    Task Shoot(IGameState gameState, IRoom room, IPlayer player);
    Task CheckObstacleCollision(IGameState gameState, IPlayer player);
    Task MoveObstacles(IGameState gameState);
    Task ResolveSnakeCollisions(IGameState gameState);
    /// <summary>
    /// Lightning on a spot: body segments within the radius break off like a broken part, a head within it dies, and the mass
    /// falls as pellets. Snakes that cannot be harmed are skipped. Returns the snakes that died, for the caller to notify and remove.
    /// </summary>
    /// <summary>Fires a shot from the boss of the room along each angle (radians, 0 to the right, clockwise on screen). Nothing without a boss.</summary>
    void FireTurret(IGameState gameState, IReadOnlyList<double> angles);

    /// <summary>
    /// Lets the shots of snakes hurt the boss: each takes one hit point and is spent (a piercing one hits once and goes on). The snake
    /// that lands the last one gets the reward: food where the boss stood, and its perk.
    /// </summary>
    Task ResolveBossHits(IGameState gameState);

    /// <summary>
    /// Makes every snake whose head is outside a circle (the short way round the world) pay: it loses up to <paramref name="mass"/> mass,
    /// which falls as food where it is, and a snake with none left dies. The invulnerable are skipped. Returns the snakes that died.
    /// </summary>
    IReadOnlyList<IPlayer> ApplyZoneDamage(IGameState gameState, double x, double y, double radius, long mass);

    /// <param name="kind">What falls: a snake that loses mass to it counts a hit of this kind in its stats.</param>
    IReadOnlyList<IPlayer> StrikeArea(IGameState gameState, double x, double y, double radius, WorldStrikeKind kind = WorldStrikeKind.Lightning);
    IPlayer? GetPlayer(IGameState gameState, ITrackableId trackableId);

    /// <summary>
    /// Tags a player with the account it signed in with: from now on the stats of its life are counted, and they are reported
    /// (<see cref="IStatsReporter"/>) once, when the player is removed (<see cref="DisconnectPlayer"/>), whether it died or left.
    /// </summary>
    void AttachAccount(IPlayer player, Guid accountId);

    /// <summary>The stats of a signed-in snake that died and is not removed yet (they are reported then); null otherwise.</summary>
    LifeStatsReport? FinishedStatsOf(IPlayer player);
}