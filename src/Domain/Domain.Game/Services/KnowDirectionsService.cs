using Domain.Game.Extensions;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Domain.Game.Services;

public class KnowDirectionsService: IKnowDirectionsService
{
    private readonly IReadOnlyDictionary<Direction, IPointAxis> _directions;

    public KnowDirectionsService(IReadOnlyDictionary<Direction, IPointAxis> directions)
    {
        _directions = directions;
    }

    public int Count => _directions.Count;
    public bool Knows(int direction)
    {
        try
        {
            return Knows(direction.ToDirectionsEnum());
        }
        catch (Exception e)
        {
            return false;
        }
    }

    public KeyValuePair<Direction, IPointAxis>? Get(int direction)
    {
        try
        {
            return Get(direction.ToDirectionsEnum());
        }
        catch (Exception e)
        {
            return null;
        }
    }

    public bool Knows(Direction direction)
    {
        return _directions.ContainsKey(direction);
    }

    public KeyValuePair<Direction, IPointAxis>? Get(Direction direction)
    {
        if (_directions.TryGetValue(direction, out var pointAxis))
            return new KeyValuePair<Direction, IPointAxis>(direction, pointAxis);

        return null;
    }
}