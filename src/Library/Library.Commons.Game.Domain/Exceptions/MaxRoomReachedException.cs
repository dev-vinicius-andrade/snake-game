namespace Library.Commons.Game.Domain.Exceptions;

public class MaxRoomReachedException:Exception
{
    public const string DefaultMessageTemplate = "Max of {0} room reached";
    public MaxRoomReachedException(long maxRooms) : base(string.Format(DefaultMessageTemplate, maxRooms))
    {
    }
}