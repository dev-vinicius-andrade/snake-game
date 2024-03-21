namespace Library.Commons.Game.Domain.Exceptions;

public class UnableToCreateRoomException : Exception
{
    public const string UnableToCreateRoomMessage = "Unable to create room";
    public UnableToCreateRoomException() : base(UnableToCreateRoomMessage)
    {
    }

}