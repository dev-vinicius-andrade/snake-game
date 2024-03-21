namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface IScore
{
    long CurrentScore { get; set; }
    string Nickname { get; set; }
    
}