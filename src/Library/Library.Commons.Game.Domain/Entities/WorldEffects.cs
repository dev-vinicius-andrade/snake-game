namespace Library.Commons.Game.Domain.Entities;

/// <summary>A mine lying on the map of a room until a snake touches it. It is shown from the start and can only be set off once armed.</summary>
/// <param name="Radius">World units around the mine that its blast reaches.</param>
/// <param name="TriggerRadius">How close a head must come to set it off.</param>
/// <param name="ArmedAt">The moment it can be set off.</param>
public sealed record WorldMine(Guid Id, double X, double Y, double Radius, double TriggerRadius, DateTimeOffset ArmedAt);

/// <summary>A snake that is wanted: whoever kills it is paid. It ends when the snake dies or the time is up.</summary>
/// <param name="Target">The guid of the wanted snake.</param>
/// <param name="EndsAt">When the mark is taken off if nobody killed it.</param>
/// <param name="RewardMass">Mass the killer gets.</param>
/// <param name="FoodsCredit">Foods the kill counts for toward the perks of the killer.</param>
public sealed record WorldBounty(Guid Target, DateTimeOffset EndsAt, long RewardMass, int FoodsCredit);
