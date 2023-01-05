using Library.Commons.Api.Entities.Configurations;
using Library.Extensions.DependencyInjection.Abstractions;
using System.Text.Json.Serialization;

namespace Application.Game.Server.Entities.Configurartions;

public class AppSettings:BaseAppSettings
{
    //[JsonPropertyName("gameServerConfiguration")]
    //public GameServerConfiguration GameServerConfiguration { get; set; }
    [JsonPropertyName("swaggerConfiguration")]
    public SwaggerConfiguration SwaggerConfiguration { get; set; }
    [JsonPropertyName("corsConfiguration")]
    public CorsConfiguration CorsConfiguration { get; set; }

}