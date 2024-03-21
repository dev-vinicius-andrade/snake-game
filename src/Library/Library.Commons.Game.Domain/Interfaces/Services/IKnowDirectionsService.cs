using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Domain.Interfaces.Services;

public interface IKnowDirectionsService
{
    int Count { get; }
    bool Knows(int direction);
    KeyValuePair<Direction, IPointAxis>? Get(int direction);
    bool Knows(Direction direction);
    KeyValuePair<Direction, IPointAxis>? Get(Direction direction);

}