using System.Text.Json.Serialization;
using Library.Extensions.DependencyInjection.Abstractions;

namespace Application.Service.Orchestrator.Entities.Configurations;

public class AppSettings : BaseAppSettings
{
    [JsonPropertyName("dockerDeamonConfiguration")]
    public DockerDeamonConfiguration DockerDeamonConfiguration { get; set; } = null!;
    [JsonPropertyName("gameServerConfiguration")]
    public GameServerConfiguration GameServerConfiguration { get; set; } = null!;

}