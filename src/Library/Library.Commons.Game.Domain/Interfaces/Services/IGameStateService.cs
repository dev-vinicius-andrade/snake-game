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
    bool ChangeDirectionByPlayerId(string playerConnectionId, Direction? direction = null);
    Task MovePlayer(IGameState gameState, IPlayer player);
    bool CanGenerateFood(IGameState gameState);
    Task<(IPlayer player,string oldPlayerConnectionId)?> ReconnectPlayer(ITrackableId roomTrackableId, ITrackableId playerTrackableId, string playerConnectionId);
    Task<bool> DisconnectPlayer(ITrackableId roomTrackableId, ITrackableId playerTrackableId);
    Task CheckFoodCollision(IGameState gameState, IPlayer player);
    Task<IList<IScore>> GetScore(IGameState gameState, int skip=0, int take=10);
    Task Shoot(IGameState gameState, IRoom room, IPlayer player);
    Task CheckObstacleCollision(IGameState gameState, IPlayer player);
    Task MoveObstacles(IGameState gameState);
    IPlayer? GetPlayer(IGameState gameState, ITrackableId trackableId);
}