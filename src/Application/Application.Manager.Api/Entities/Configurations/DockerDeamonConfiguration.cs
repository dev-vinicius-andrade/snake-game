using System.Text.Json.Serialization;

namespace Application.Manager.Api.Entities.Configurations;

public class DockerDeamonConfiguration
{
    [JsonPropertyName("endpoint")]
    public string EndPoint { get; set; }
}