using System.Text.Json.Serialization;

namespace Library.Commons.Api.Entities.Requests.Room;

/// <summary>Sent by a game server when a private room was made for a player.</summary>
public class RoomCreatedRequest
{
    [JsonPropertyName("roomId")]
    public Guid RoomId { get; set; }
    [JsonPropertyName("code")]
    public string Code { get; set; } = null!;
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("maxPlayers")]
    public int MaxPlayers { get; set; }
    [JsonPropertyName("gameServerUrl")]
    public string GameServerUrl { get; set; } = null!;
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = null!;
}

/// <summary>Why a join or a room creation did not work, for the player.</summary>
public class FailureRequest
{
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = null!;
}

public class RoomSummary
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    [JsonPropertyName("code")]
    public string? Code { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("isPrivate")]
    public bool IsPrivate { get; set; }
    [JsonPropertyName("players")]
    public int Players { get; set; }
    [JsonPropertyName("maxPlayers")]
    public int MaxPlayers { get; set; }
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>What a game server sends every few seconds: all of its rooms. Rooms missing from it are gone.</summary>
public class RoomHeartbeatRequest
{
    [JsonPropertyName("gameServerUrl")]
    public string GameServerUrl { get; set; } = null!;
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = null!;
    [JsonPropertyName("rooms")]
    public List<RoomSummary> Rooms { get; set; } = new();
}
