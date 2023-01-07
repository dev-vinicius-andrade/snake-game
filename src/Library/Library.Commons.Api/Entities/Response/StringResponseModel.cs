using Library.Commons.Api.Entities.Response.Abstractions;
using System.Text.Json.Serialization;

namespace Library.Commons.Api.Entities.Response;

public class StringResponseModel : BaseResponseModel
{
    public StringResponseModel() : base(false)
    {
    }

    public StringResponseModel(string message = null, bool hasError = false) : base(hasError, message)
    {
    }

    [JsonPropertyName("data")]
    public string Data { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }
}