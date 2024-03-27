using System.Text.Json.Serialization;

namespace Application.Service.Orchestrator.Entities.Configurations;

public class GameServerConfiguration
{
    [JsonPropertyName("image")]
    public string Image { get; set; } = null!;

    [JsonPropertyName("internalPort")]
    public int InternalPort { get; set; }
    [JsonPropertyName("scheme")]
    public string Scheme { get; set; } = null!;

    [JsonPropertyName("domain")]
    public string Domain { get; set; } = null!;
}