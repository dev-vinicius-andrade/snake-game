using System.Text.Json.Serialization;

namespace Library.Commons.Entities.Configurations;

public class ApiIntegrationConfiguration
{
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; }
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = null!;
    
}