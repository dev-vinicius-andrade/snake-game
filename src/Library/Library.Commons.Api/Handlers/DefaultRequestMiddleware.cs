using Library.Commons.Api.Entities;
using Library.Commons.Api.Interfaces;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Library.Commons.Api.Handlers;

public class DefaultRequestMiddleware : IMiddleware
{
    private readonly IDefaultRequestExceptionHandler _defaultRequestExceptionHandler;
    private readonly IRequestExceptionHandler _exceptionHandler;
    private readonly ILogger _logger;

    public DefaultRequestMiddleware(
        IDefaultRequestExceptionHandler defaultRequestExceptionHandler = null,
        IRequestExceptionHandler exceptionHandler = null,
        ILogger logger = null)
    {
        _defaultRequestExceptionHandler = defaultRequestExceptionHandler;
        _exceptionHandler = exceptionHandler;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, ex.Message);

            if ((await HandleExceptionAsync(ex, context)).Handled) return;
            if (_defaultRequestExceptionHandler == null) throw;
            await _defaultRequestExceptionHandler.HandleExceptionAsync(context, ex, context.RequestAborted);
        }
    }

    private async Task<IRequestExceptionHandlerStatus> HandleExceptionAsync(Exception ex, HttpContext context)
    {
        if (_exceptionHandler == null) return new RequestExceptionHandlerStatus(false);
        return await _exceptionHandler.HandleExceptionAsync(context, ex, context.RequestAborted);
    }
}