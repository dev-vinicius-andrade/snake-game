using System.Net;
using Library.Commons.Api.Extensions;

namespace Library.Commons.Api.Entities.Response.Abstractions;

public class BaseResponseModel
{
    protected BaseResponseModel(bool hasError, string message = null)
    {
        this.HasError = hasError;
#pragma warning disable CS8601
        this.Message = message.IsNullOrEmptyOrWhiteSpace() ? (hasError ? "Error" : "Success") : message;
#pragma warning restore CS8601
    }

    protected HttpStatusCode StatusCode { get; set; }

    public bool HasError { get; }

    public string Message { get; set; }  
}