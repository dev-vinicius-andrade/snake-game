using System.Net;
using Library.Commons.Api.Interfaces;

namespace Library.Commons.Api.Entities;

public class RequestExceptionHandlerStatus : ICompleteRequestExceptionHandlerStatus
{
    public RequestExceptionHandlerStatus(bool handled = true)
    {
        Handled = handled;
    }

    public RequestExceptionHandlerStatus(HttpStatusCode httpStatusCode, Exception exception, bool handled = true) :
        this(handled)
    {
        HttpStatusCode = httpStatusCode;
        Exception = exception;
    }

    public HttpStatusCode HttpStatusCode { get; }
    public Exception Exception { get; }
    public bool Handled { get; }
}