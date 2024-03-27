using System.ComponentModel;
using System.Text.Json.Serialization;
using Library.Commons.Api.Entities.Configurations;
using Library.Commons.Entities.Configurations;
using Library.Commons.Eventbus.RabbitMq.Configurations;
using Library.Extensions.DependencyInjection.Abstractions;

namespace Application.Game.Server.Entities.Configurations;

public class AppSettings:BaseAppSettings
{

    [JsonPropertyName("corsConfiguration")]
    public CorsConfiguration CorsConfiguration { get; set; } = null!;
    [JsonPropertyName("eventbusConfiguration")]
    public EventbusConfiguration EventbusConfiguration { get; set; } = null!;
    [JsonPropertyName("managementApiConfiguration")]
    public ApiIntegrationConfiguration ManagementApiConfiguration { get; set; } = null!;
}