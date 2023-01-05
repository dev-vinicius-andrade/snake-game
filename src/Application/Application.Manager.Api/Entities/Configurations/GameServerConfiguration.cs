using System.Text.Json.Serialization;

namespace Application.Manager.Api.Entities.Configurations;

public class GameServerConfiguration
{
    [JsonPropertyName("image")]
    public string Image { get; set; }
    [JsonPropertyName("internalPort")]
    public int InternalPort { get; set; }
    [JsonPropertyName("scheme")]
    public string Scheme { get; set; }
    [JsonPropertyName("domain")]
    public string Domain { get; set; }
    
}