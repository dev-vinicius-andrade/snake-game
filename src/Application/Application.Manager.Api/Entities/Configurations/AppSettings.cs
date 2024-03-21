using System.Text.Json.Serialization;
using Library.Commons.Api.Entities.Configurations;
using Library.Commons.Eventbus.RabbitMq.Configurations;
using Library.Extensions.DependencyInjection.Abstractions;

namespace Application.Manager.Api.Entities.Configurations;

public class AppSettings : BaseAppSettings
{
    [JsonPropertyName("dockerDeamonConfiguration")]
    public DockerDeamonConfiguration DockerDeamonConfiguration { get; set; } = null!;

    [JsonPropertyName("gameServerConfiguration")]
    public GameServerConfiguration GameServerConfiguration { get; set; } = null!;

    [JsonPropertyName("swaggerConfiguration")]
    public SwaggerConfiguration SwaggerConfiguration { get; set; } = null!;

    [JsonPropertyName("corsConfiguration")]
    public CorsConfiguration CorsConfiguration { get; set; } = null!;

    [JsonPropertyName("auth")] public AuthConfiguration? Auth { get; set; }

    [JsonPropertyName("redisConfiguration")]
    public RedisConfiguration RedisConfiguration { get; set; } = null!;
    [JsonPropertyName("eventbusConfiguration")]
    public EventbusConfiguration EventbusConfiguration { get; set; } = null!;
}