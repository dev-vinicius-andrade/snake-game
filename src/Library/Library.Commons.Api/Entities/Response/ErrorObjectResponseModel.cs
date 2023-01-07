using Library.Commons.Api.Entities.Response.Abstractions;
using System.Text.Json.Serialization;

namespace Library.Commons.Api.Entities.Response;

public class ErrorObjectResponseModel<TObject> : BaseErrorResponseModel
    where TObject : class
{
    [JsonPropertyName("data")]
    public TObject Data { get; set; } = null!;
}