using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;

namespace KasseAPI_Final.Middleware
{
    /// <summary>
    /// Ensures every request has a correlation ID for tracing. Reads X-Correlation-Id from request or generates one.
    /// Propagates to HttpContext.Items and response header for audit and client logging.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        public const string CorrelationIdItemKey = "CorrelationId";
        public const string CorrelationIdHeaderName = "X-Correlation-Id";

        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(correlationId))
                correlationId = System.Guid.NewGuid().ToString("N");

            context.Items[CorrelationIdItemKey] = correlationId;
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
                    context.Response.Headers.Append(CorrelationIdHeaderName, correlationId);
                return Task.CompletedTask;
            });

            await _next(context);
        }
    }
}
