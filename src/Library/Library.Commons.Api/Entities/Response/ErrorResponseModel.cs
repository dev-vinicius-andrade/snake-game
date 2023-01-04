using System.Net;
using Library.Commons.Api.Entities.Response.Abstractions;
using Library.Commons.Api.Extensions;

namespace Library.Commons.Api.Entities.Response;

public class ErrorResponseModel:BaseErrorResponseModel

{
    public ErrorResponseModel(HttpStatusCode statusCode, Exception ex)
    {
        this.StatusCode = statusCode;
        this.Message = ex.Message;
    }

    public ErrorResponseModel(HttpStatusCode statusCode, string message = null)
    {
        this.StatusCode = statusCode;
#pragma warning disable CS8601
        this.Message = message.IsNullOrEmptyOrWhiteSpace() ? statusCode.ToString() : message;
#pragma warning restore CS8601
    }

    public ErrorResponseModel(Exception ex, HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
    {
        this.StatusCode = statusCode;
        this.Message = ex.Message;
    }
}