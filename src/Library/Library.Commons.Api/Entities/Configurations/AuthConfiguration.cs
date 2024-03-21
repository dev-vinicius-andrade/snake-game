using System.Text.Json.Serialization;

namespace Library.Commons.Api.Entities.Configurations;

public class AuthConfiguration
{
    public const string SectionName = nameof(AuthConfiguration);
    [JsonPropertyName("apiKeys")]
    public List<string>? ApiKeys { get; set; }
}