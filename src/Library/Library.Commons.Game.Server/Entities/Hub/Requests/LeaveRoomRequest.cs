using System;
using System.Text.Json.Serialization;

namespace Library.Commons.Game.Server.Entities.Hub.Requests;

public record LeaveRoomRequest
{
    [JsonPropertyName("roomId")]
    public Guid RoomId { get; init; }
    [JsonPropertyName("playerTrackableId")]
    public Guid PlayerTrackableId { get; init; }
}