using System.Data;
using System.Net;
using Library.Commons.Api.Abstractions;
using Library.Commons.Api.Exceptions;
using Library.Commons.Game.Domain.Exceptions;

namespace Application.Game.Server.Handlers;

public class ExceptionHandler:BaseRequestExceptionHandler
{
    protected override HttpStatusCode GetStatusCodeFromNotHandledExceptionType(Exception exception)
    {
        return exception switch
        {
            NotFoundException => HttpStatusCode.NoContent,
            ArgumentNullException => HttpStatusCode.UnprocessableEntity,
            ArgumentOutOfRangeException => HttpStatusCode.UnprocessableEntity,
            ModelStateException => HttpStatusCode.UnprocessableEntity,
            DuplicateNameException => HttpStatusCode.Conflict,
            _ => HttpStatusCode.InternalServerError
        };
    }
}