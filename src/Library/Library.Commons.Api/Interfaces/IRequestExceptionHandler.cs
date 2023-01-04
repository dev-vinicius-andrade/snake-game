using System.Net;
using Microsoft.AspNetCore.Http;

namespace Library.Commons.Api.Interfaces;

public interface IRequestExceptionHandler
{
    Task<IRequestExceptionHandlerStatus> HandleExceptionAsync(HttpContext context, Exception exception, CancellationToken cancellationToken = default);
    HttpStatusCode StatusCodeFromExceptionType(Exception exception);
}