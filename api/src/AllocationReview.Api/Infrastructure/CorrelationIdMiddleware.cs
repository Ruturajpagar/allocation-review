using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AllocationReview.Api.Infrastructure;

/// <summary>
/// Gives every request a correlation ID. A caller-supplied X-Correlation-ID is reused when it is
/// safe (short, plain characters); otherwise a new one is created. The ID is returned in the
/// response header, added to every log line through a logging scope and to error responses,
/// so a user can quote it and support can find the matching logs.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(correlationId) || !SafeId().IsMatch(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.Items[ItemKey] = correlationId;
        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using var scope = logger.BeginScope("CorrelationId:{CorrelationId}", correlationId);

        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            // Method, route and status only: no query string, headers, cookies or bodies.
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var level = context.Response.StatusCode >= 500 ? LogLevel.Error
                : context.Response.StatusCode >= 400 ? LogLevel.Warning
                : LogLevel.Information;
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                logger.Log(level, "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0.0} ms",
                    context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, elapsedMs);
            }
        }
    }

    public static string? Get(HttpContext context) => context.Items[ItemKey] as string;

    [GeneratedRegex("^[A-Za-z0-9-]{1,64}$")]
    private static partial Regex SafeId();
}
