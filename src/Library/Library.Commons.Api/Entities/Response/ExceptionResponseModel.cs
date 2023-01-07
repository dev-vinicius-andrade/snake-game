using System.Net;

namespace Library.Commons.Api.Entities.Response;

public class ExceptionResponseModel : ErrorResponseModel
{
    public ExceptionResponseModel(HttpStatusCode statusCode, Exception exception, string message = null) : base(statusCode,
        $"{(string.IsNullOrWhiteSpace(message) ? string.Empty : " - ")}{exception.Message}")
    {
        Type = exception.GetType().ToString();
        Stacktrace = exception.ToString();
    }

    public string Type { get; set; }
    public string Stacktrace { get; set; }
}