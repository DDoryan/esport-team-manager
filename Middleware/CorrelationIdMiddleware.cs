using System.Diagnostics;

namespace RepriseWeb.Middleware;

public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeaderName = "X-Correlation-ID";

    private readonly RequestDelegate _next;

    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;

            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await _next(context);
        }
    }
}