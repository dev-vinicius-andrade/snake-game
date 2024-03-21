using Library.Commons.Api.Entities.Response.Abstractions;
using System.Text.Json.Serialization;

namespace Library.Commons.Api.Entities.Response;

public class BooleanResponseModel : BaseResponseModel
{
    public BooleanResponseModel() : base(false)
    {
    }

    public BooleanResponseModel(string message = null, bool hasError = false) : base(hasError, message)
    {
    }

    [JsonPropertyName("data")]
    public bool Data { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }
}