using System.Text.Json.Serialization;

namespace Library.Commons.Api.Entities.Requests.Join;

public class JoinRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;
    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = null!;
}