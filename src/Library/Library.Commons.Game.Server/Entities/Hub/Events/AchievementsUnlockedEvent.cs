using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Library.Commons.Game.Server.Entities.Hub.Events;

/// <summary>Achievements the account of the player just unlocked (docs/ACHIEVEMENTS.md). Sent to its connections only.</summary>
public record AchievementsUnlockedEvent
{
    /// <summary>Ids of <c>Achievements.Defaults</c>; the client has their names and descriptions.</summary>
    [JsonPropertyName("ids")]
    public IReadOnlyList<string> Ids { get; set; } = [];
}
