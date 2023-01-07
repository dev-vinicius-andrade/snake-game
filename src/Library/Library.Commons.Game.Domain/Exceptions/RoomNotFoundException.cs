namespace Library.Commons.Game.Domain.Exceptions;

public class RoomNotFoundException:NotFoundException
{
    public const string DefaultMessageTemplate = "Room {0} not found";
    public RoomNotFoundException(Guid id) : base(string.Format(DefaultMessageTemplate, id))
    {
    }
}