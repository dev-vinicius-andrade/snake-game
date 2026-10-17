using System.Text.Json.Serialization;

namespace Library.Commons.Api.Entities.Configurations;

public class AuthConfiguration
{
    public const string SectionName = nameof(AuthConfiguration);
    [JsonPropertyName("apiKeys")]
    public List<string>? ApiKeys { get; set; }

    /// <summary>Keys that may use the admin endpoints. Empty means the admin endpoints are closed to everyone.</summary>
    [JsonPropertyName("adminApiKeys")]
    public List<string>? AdminApiKeys { get; set; }
}