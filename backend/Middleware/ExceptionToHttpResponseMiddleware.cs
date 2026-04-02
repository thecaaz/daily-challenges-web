using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DailyChallenges.Middleware
{
    public class ExceptionToHttpResponseMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionToHttpResponseMiddleware> _logger;

        public ExceptionToHttpResponseMiddleware(RequestDelegate next, ILogger<ExceptionToHttpResponseMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while processing request");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            var payload = new { message = ex.Message };

            int status;
            switch (ex)
            {
                case KeyNotFoundException _:
                    status = StatusCodes.Status404NotFound;
                    break;
                case UnauthorizedAccessException _:
                    status = StatusCodes.Status401Unauthorized;
                    break;
                case ArgumentException _:
                    status = StatusCodes.Status400BadRequest;
                    break;
                case InvalidOperationException ioe:
                    // preserve existing behavior where some InvalidOperationExceptions are treated as Conflict
                    if (!string.IsNullOrEmpty(ioe.Message) && ioe.Message.Contains("already submitted"))
                        status = StatusCodes.Status409Conflict;
                    else
                        status = StatusCodes.Status400BadRequest;
                    break;
                default:
                    status = StatusCodes.Status500InternalServerError;
                    break;
            }

            var result = JsonSerializer.Serialize(payload);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = status;
            return context.Response.WriteAsync(result);
        }
    }

    public static class ExceptionToHttpResponseMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionToHttpResponse(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionToHttpResponseMiddleware>();
        }
    }
}
