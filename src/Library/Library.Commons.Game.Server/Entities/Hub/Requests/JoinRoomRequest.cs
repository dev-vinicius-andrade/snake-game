using System;
using System.Text.Json.Serialization;

namespace Library.Commons.Game.Server.Entities.Hub.Requests;

public class  JoinRoomRequest
{
    [JsonPropertyName("roomId")]
    public Guid RoomId { get; set; }
    [JsonPropertyName("playerTrackableId")]
    public Guid? PlayerTrackableId { get; set; }
    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = null!;
}