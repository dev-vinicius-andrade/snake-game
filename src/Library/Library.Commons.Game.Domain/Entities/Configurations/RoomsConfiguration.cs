using System.Text.Json.Serialization;

namespace Library.Commons.Game.Domain.Entities.Configurations;

public record RoomsConfiguration
{
    [JsonPropertyName("maxRooms")]
    public int MaxRooms { get; set; }
    [JsonPropertyName("maxPlayersPerRoom")]
    public int MaxPlayersPerRoom { get; set; }

    [JsonPropertyName("maxFoods")]
    public int MaxFoods { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }
    [JsonPropertyName("height")]
    public int? Height { get; set; }
}