namespace Library.Commons.Game.Domain.Entities;

/// <summary>
/// What one life of a signed-in snake did, as the game server sends it to the manager (docs/ACHIEVEMENTS.md). Keys are
/// <see cref="StatKeys"/>. The manager applies a report once: sending it again with the same <paramref name="ReportId"/> changes nothing.
/// </summary>
/// <param name="ReportId">Made once per life; the same on every retry.</param>
/// <param name="Counters">How much the life added to each counter.</param>
/// <param name="Maxima">The best of the life for each maximum.</param>
public sealed record LifeStatsReport(Guid ReportId, IReadOnlyDictionary<string, long> Counters, IReadOnlyDictionary<string, long> Maxima);
