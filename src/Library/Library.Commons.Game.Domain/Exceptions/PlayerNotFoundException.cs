namespace Library.Commons.Game.Domain.Exceptions;

public class PlayerNotFoundException:NotFoundException
{
    public const string DefaultMessageTemplate = "Player {0} not found";
    public PlayerNotFoundException(Guid id) : base(string.Format(DefaultMessageTemplate, id))
    {
    }
}