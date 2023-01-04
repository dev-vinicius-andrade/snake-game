using System.Net;

namespace Library.Commons.Api.Interfaces;

public interface ICompleteRequestExceptionHandlerStatus : IRequestExceptionHandlerStatus
{
    HttpStatusCode HttpStatusCode { get; }
    Exception Exception { get; }
}