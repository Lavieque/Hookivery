using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hookivery.Api.ErrorHandling;

internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var method = httpContext.Request.Method;
        var path = httpContext.Request.Path.Value ?? "/";

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            LogRequestAborted(logger, method, path);
            httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return true;
        }

        int status;
        string title;

        if (exception is BadHttpRequestException badRequest)
        {
            status = badRequest.StatusCode;
            title = "The request is invalid.";
            LogBadRequest(logger, exception, method, path, status);
        }
        else
        {
            status = StatusCodes.Status500InternalServerError;
            title = "An unexpected error occurred.";
            LogUnhandledException(logger, exception, method, path);
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = status, Title = title },
        });
    }

    [LoggerMessage(EventId = 1000, Level = LogLevel.Error,
        Message = "Unhandled exception while processing {Method} {Path}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string method, string path);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning,
        Message = "Bad request {Method} {Path} answered with {StatusCode}")]
    private static partial void LogBadRequest(ILogger logger, Exception exception, string method, string path, int statusCode);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information,
        Message = "Request {Method} {Path} aborted by the client")]
    private static partial void LogRequestAborted(ILogger logger, string method, string path);
}
