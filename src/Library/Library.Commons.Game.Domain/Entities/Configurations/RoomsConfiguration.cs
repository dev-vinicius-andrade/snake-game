using System.Text.Json.Serialization;

namespace Library.Commons.Game.Domain.Entities.Configurations;

public record RoomsConfiguration
{
    [JsonPropertyName("maxRooms")]
    public int MaxRooms { get; set; }
    [JsonPropertyName("maxPlayersPerRoom")]
    public int MaxPlayersPerRoom { get; set; }
}