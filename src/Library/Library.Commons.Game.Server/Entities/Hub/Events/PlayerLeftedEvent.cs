using System;
using System.Text.Json.Serialization;

namespace Library.Commons.Game.Server.Entities.Hub.Events;

public record PlayerLeftedEvent
{
    public const string EventName = "PlayerLefted";
    [JsonPropertyName("playerTrackableId")]
    public Guid PlayerTrackableId { get; set; }
    [JsonPropertyName("roomId")]
    public Guid RoomId { get; set; }
    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = null!;
    
}