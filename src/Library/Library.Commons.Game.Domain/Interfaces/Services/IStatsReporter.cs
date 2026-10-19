using Library.Commons.Game.Domain.Entities;

namespace Library.Commons.Game.Domain.Interfaces.Services;

/// <summary>Sends the stats of a life that ended to the account. Never blocks and never throws: called from the game loop.</summary>
public interface IStatsReporter
{
    void Report(Guid accountId, LifeStatsReport report);
}
