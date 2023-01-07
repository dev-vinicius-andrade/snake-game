using System.Data;
using System.Net;
using System.Net.Mime;
using System.Runtime.Serialization;
using System.Text.Json;
using Library.Commons.Api.Entities;
using Library.Commons.Api.Entities.Response;
using Library.Commons.Api.Exceptions;
using Library.Commons.Api.Extensions;
using Library.Commons.Api.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Library.Commons.Api.Abstractions;

public abstract class BaseRequestExceptionHandler : IRequestExceptionHandler
{
    public virtual async Task<IRequestExceptionHandlerStatus> HandleExceptionAsync(HttpContext context, Exception exception, CancellationToken cancellationToken = default)
    {
        var httpStatusCode = StatusCodeFromExceptionType(exception);

        context.Response.StatusCode = httpStatusCode.ToInt();
        context.Response.ContentType = ResponseContentType();

        try
        {
            switch (exception)
            {
                case ModelStateException stateException:
                    await HandleModelStateExceptionAsync(context, stateException, httpStatusCode, cancellationToken);
                    break;
                default:
                    await HandleExceptionAsync(context, exception, httpStatusCode, cancellationToken);
                    break;
            }

            return new RequestExceptionHandlerStatus(httpStatusCode, exception);
        }
        catch (SerializationException ex)
        {
            return await HandleResponseSerializationException(context, cancellationToken, ex);
        }
    }

    public virtual HttpStatusCode StatusCodeFromExceptionType(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            ArgumentNullException => HttpStatusCode.UnprocessableEntity,
            ArgumentOutOfRangeException => HttpStatusCode.UnprocessableEntity,
            ModelStateException => HttpStatusCode.UnprocessableEntity,
            DuplicateNameException => HttpStatusCode.Conflict,
            _ => GetStatusCodeFromNotHandledExceptionType(exception)
        };
    }

    private async Task<IRequestExceptionHandlerStatus> HandleResponseSerializationException(HttpContext context, CancellationToken cancellationToken, Exception ex)
    {
        const HttpStatusCode statusCode = HttpStatusCode.InternalServerError;
        await context.Response.WriteAsync(Serialize(new ErrorResponseModel(ex)), cancellationToken);
        return new RequestExceptionHandlerStatus(statusCode, ex);
    }

    protected virtual string ResponseContentType()
    {
        return MediaTypeNames.Application.Json;
    }

    protected virtual Task HandleExceptionAsync(HttpContext context, Exception exception, HttpStatusCode httpStatusCode, CancellationToken cancellationToken = default)
    {
        return context.Response.WriteAsync(Serialize(BuildErrorResponseModelFromException(exception, httpStatusCode)), cancellationToken);
    }

    protected virtual ErrorResponseModel BuildErrorResponseModelFromException(Exception exception, HttpStatusCode httpStatusCode)
    {
        return new ErrorResponseModel(exception, httpStatusCode);
    }

    protected virtual Task HandleUpdateNotAppliedExceptionsAsync(HttpContext context)
    {
        return context.Response.CompleteAsync();
    }

    protected virtual async Task HandleModelStateExceptionAsync(HttpContext context, ModelStateException stateException, HttpStatusCode httpStatusCode,
        CancellationToken cancellationToken = default)
    {
        await context.Response.WriteAsync(Serialize(new ErrorsResponseModel(stateException, httpStatusCode){Errors = stateException.Errors}), cancellationToken);
    }

    protected abstract HttpStatusCode GetStatusCodeFromNotHandledExceptionType(Exception exception);

    protected virtual string Serialize<T>(T entity, JsonSerializerOptions options = null)
    {
        return JsonSerializer.Serialize(entity, options);
    }
}