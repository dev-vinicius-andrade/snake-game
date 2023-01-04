using System.Text.Json.Serialization;

namespace Application.Manager.Api.Entities.Configurations;

public class GameServerConfiguration
{
    [JsonPropertyName("image")]
    public string Image { get; set; }
}