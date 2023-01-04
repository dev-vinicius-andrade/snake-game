using System.Net;
using System.Text.Json.Serialization;

namespace Library.Commons.Api.Entities.Response;

public class ErrorsResponseModel: ErrorResponseModel
{
    public ErrorsResponseModel(HttpStatusCode statusCode, Exception ex)
        : base(statusCode, ex)
    {
        Errors = new List<string>();
    }

    public ErrorsResponseModel(HttpStatusCode statusCode, string message = null)
        : base(statusCode, message)
    {
        Errors = new List<string>();
    }

    public ErrorsResponseModel(Exception ex, HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
        : base(ex, statusCode)
    {
        Errors = new List<string>();
    }

    public ErrorsResponseModel(
        IReadOnlyList<string> errors,
        HttpStatusCode statusCode,
        string message = null)
        : base(statusCode, message)
    {
        Errors = errors;
    }

    [JsonPropertyName("errors")]
    public IReadOnlyList<string> Errors { get; set; }
}