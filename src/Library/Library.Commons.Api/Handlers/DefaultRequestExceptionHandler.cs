using System.Net;
using Library.Commons.Api.Abstractions;
using Library.Commons.Api.Interfaces;

namespace Library.Commons.Api.Handlers;

internal sealed class DefaultRequestExceptionHandler : BaseRequestExceptionHandler, IDefaultRequestExceptionHandler
{
    protected override HttpStatusCode GetStatusCodeFromNotHandledExceptionType(Exception exception)
    {
        return HttpStatusCode.BadRequest;
    }
}