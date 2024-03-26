using System.Text.Json.Serialization;

namespace Library.Commons.Entities.Configurations;

public class ApiIntegrationConfiguration
{
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = null!;

    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = null!;
    
}