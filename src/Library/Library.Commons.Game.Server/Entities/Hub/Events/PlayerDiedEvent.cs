using System.Text.Json.Serialization;

namespace Library.Commons.Game.Server.Entities.Hub.Events;

public record PlayerDiedEvent
{
    [JsonPropertyName("score")]
    public long Score { get; set; }
}