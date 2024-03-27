using System.Text.Json.Serialization;

namespace Application.Service.Orchestrator.Entities.Configurations;

public class DockerDeamonConfiguration
{
    [JsonPropertyName("endpoint")]
    public string EndPoint { get; set; }

    public int InternalPort { get; set; }
}