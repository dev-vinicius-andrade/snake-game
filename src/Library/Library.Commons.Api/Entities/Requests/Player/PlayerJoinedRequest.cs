using System.Text.Json.Serialization;

namespace Library.Commons.Api.Entities.Requests.Player;

public class PlayerJoinedRequest
{
    [JsonPropertyName("gameServerUrl")]
    public string GameServerUrl { get; set; } = null!;
    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = null!;
    [JsonPropertyName("roomId")]
    public Guid RoomId { get; set; }
}