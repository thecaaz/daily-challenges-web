using System.Text.Json;
using DailyChallenges.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DailyChallenges.Middleware
{
    /// <summary>
    /// Turns exceptions into <c>{ "message": ... }</c> JSON responses.
    /// Domain exceptions carry their own message; anything unexpected becomes a generic 500.
    /// </summary>
    public class ExceptionToHttpResponseMiddleware
    {
        public const string GenericErrorMessage = "An unexpected error occurred.";

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
                if (context.Response.HasStarted)
                {
                    // Too late to change the status code; let the server abort the response.
                    _logger.LogError(ex, "Unhandled exception after the response started");
                    throw;
                }

                var (status, message) = Map(ex);
                if (status >= StatusCodes.Status500InternalServerError)
                    _logger.LogError(ex, "Unhandled exception while processing request");
                else if (ex is InvalidOperationException)
                    // Temporary fallback: every expected failure should throw a domain exception instead.
                    _logger.LogWarning(ex, "InvalidOperationException mapped to 400; convert this throw site to a domain exception");
                else
                    _logger.LogDebug("Request failed with {Status}: {Message}", status, message);

                context.Response.Clear();
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = status;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { message }));
            }
        }

        private static (int Status, string Message) Map(Exception ex) => ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            ForbiddenException => (StatusCodes.Status403Forbidden, ex.Message),
            ConflictException => (StatusCodes.Status409Conflict, ex.Message),
            ValidationException => (StatusCodes.Status400BadRequest, ex.Message),
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, ex.Message),
            KeyNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            ArgumentException => (StatusCodes.Status400BadRequest, ex.Message),
            // Message withheld: unconverted throw sites are rare and framework IOEs can leak internals.
            InvalidOperationException => (StatusCodes.Status400BadRequest, "The request could not be completed."),
            _ => (StatusCodes.Status500InternalServerError, GenericErrorMessage)
        };
    }

    public static class ExceptionToHttpResponseMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionToHttpResponse(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionToHttpResponseMiddleware>();
        }
    }
}
