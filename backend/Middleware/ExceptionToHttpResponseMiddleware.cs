using System.Text.Json;
using DailyChallenges.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DailyChallenges.Middleware;

/// <summary>
/// Turns exceptions into <c>{ "message": ... }</c> JSON responses.
/// Domain exceptions carry their own message; anything unexpected becomes a generic 500.
/// Once the response has started the status code can no longer change, so the exception is
/// logged and rethrown and the server aborts the response.
/// </summary>
public sealed class ExceptionToHttpResponseMiddleware(RequestDelegate next, ILogger<ExceptionToHttpResponseMiddleware> logger)
{
    public const string GenericErrorMessage = "An unexpected error occurred.";

    /// <summary>Message withheld: unconverted throw sites are rare and framework IOEs can leak internals.</summary>
    private const string InvalidOperationMessage = "The request could not be completed.";

    private const string JsonContentType = "application/json";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                logger.LogError(ex, "Unhandled exception after the response started");
                throw;
            }

            var (status, message) = Map(ex);
            Log(ex, status, message);
            await WriteErrorAsync(context.Response, status, message);
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
        InvalidOperationException => (StatusCodes.Status400BadRequest, InvalidOperationMessage),
        _ => (StatusCodes.Status500InternalServerError, GenericErrorMessage)
    };

    /// <summary>
    /// InvalidOperationException is a temporary fallback to 400: every expected failure should throw a
    /// domain exception instead, so it is logged as a warning to find the remaining throw sites.
    /// </summary>
    private void Log(Exception ex, int status, string message)
    {
        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(ex, "Unhandled exception while processing request");
        else if (ex is InvalidOperationException)
            logger.LogWarning(ex, "InvalidOperationException mapped to 400; convert this throw site to a domain exception");
        else
            logger.LogDebug("Request failed with {Status}: {Message}", status, message);
    }

    private static async Task WriteErrorAsync(HttpResponse response, int status, string message)
    {
        response.Clear();
        response.ContentType = JsonContentType;
        response.StatusCode = status;
        await response.WriteAsync(JsonSerializer.Serialize(new { message }));
    }
}

public static class ExceptionToHttpResponseMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionToHttpResponse(this IApplicationBuilder builder) =>
        builder.UseMiddleware<ExceptionToHttpResponseMiddleware>();
}
