using System;
using System.Text.Json.Serialization;
using Library.Commons.Game.Domain.Entities;

namespace Library.Commons.Game.Server.Entities.Hub.Events;

/// <summary>A shot did damage. Sent to the shooter and to the snake that was hit.</summary>
public record DamageDealtEvent
{
    [JsonPropertyName("shooterGuid")]
    public Guid ShooterGuid { get; set; }

    [JsonPropertyName("targetGuid")]
    public Guid TargetGuid { get; set; }

    /// <summary>World position of the segment that was hit.</summary>
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    /// <summary>HP taken from the segment; 0 for a kill.</summary>
    [JsonPropertyName("damage")]
    public double Damage { get; set; }

    [JsonPropertyName("kind")]
    public DamageKind Kind { get; set; }
}
