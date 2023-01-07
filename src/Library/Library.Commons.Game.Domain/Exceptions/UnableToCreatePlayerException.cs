namespace Library.Commons.Game.Domain.Exceptions;

public class UnableToCreatePlayerException:Exception
{
    public const string UnableToCreatePlayerMessage = "Unable to create player";
    public UnableToCreatePlayerException() : base(UnableToCreatePlayerMessage)
    {
    }

}