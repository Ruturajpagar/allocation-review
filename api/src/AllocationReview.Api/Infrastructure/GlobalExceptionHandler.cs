using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AllocationReview.Api.Infrastructure;

/// <summary>
/// Turns any unhandled exception into a controlled 500 ProblemDetails response.
/// The exception (with stack trace) goes to the log; the client only gets a generic
/// message and the correlation ID to quote.
/// </summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client went away; nothing useful to send back.
            logger.LogInformation("Request was cancelled by the client");
            httpContext.Response.StatusCode = 499;
            return true;
        }

        logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path.Value);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Something went wrong",
                Detail = "The request could not be completed. Quote the correlation ID if you contact support.",
            },
        });
    }
}

public static class ProblemDetailsSetup
{
    /// <summary>Every error response (400/401/403/404/500) carries the correlation ID.</summary>
    public static IServiceCollection AddAppProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var correlationId = CorrelationIdMiddleware.Get(context.HttpContext);
                if (correlationId is not null)
                {
                    context.ProblemDetails.Extensions["correlationId"] = correlationId;
                }

                // Never leak exception details to clients.
                context.ProblemDetails.Extensions.Remove("exception");
            };
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }
}
