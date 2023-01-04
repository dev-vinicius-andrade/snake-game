using System.Text.Json.Serialization;
using Library.Commons.Api.Entities.Configurations;
using Library.Extensions.DependencyInjection.Abstractions;
using Microsoft.AspNetCore.Cors.Infrastructure;

namespace Application.Manager.Api.Entities.Configurations;

public class AppSettings:BaseAppSettings
{
    [JsonPropertyName("dockerDeamonConfiguration")]
    public DockerDeamonConfiguration DockerDeamonConfiguration { get; set; }
    [JsonPropertyName("gameServerConfiguration")]
    public GameServerConfiguration GameServerConfiguration { get; set; }
    [JsonPropertyName("swaggerConfiguration")]
    public SwaggerConfiguration SwaggerConfiguration { get; set; }
    [JsonPropertyName("corsConfiguration")]
    public CorsConfiguration CorsConfiguration { get; set; }
}