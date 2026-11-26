using System.Text.Json;
using DailyChallenges.Exceptions;
using DailyChallenges.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DailyChallenges.Tests;

public class ExceptionToHttpResponseMiddlewareTests
{
    private static async Task<(int Status, string? Message, string? ContentType)> RunAsync(Exception ex)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionToHttpResponseMiddleware(_ => throw ex, NullLogger<ExceptionToHttpResponseMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, doc.RootElement.GetProperty("message").GetString(), context.Response.ContentType);
    }

    public static TheoryData<Exception, int> DomainMappings => new()
    {
        { new NotFoundException("nf"), 404 },
        { new ForbiddenException("fb"), 403 },
        { new ConflictException("cf"), 409 },
        { new ValidationException("va"), 400 },
        { new AuthenticationFailedException("af"), 401 },
        { new KeyNotFoundException("knf"), 404 },
        { new ArgumentException("arg"), 400 },
        { new ArgumentNullException("param", "argnull"), 400 },
    };

    [Theory]
    [MemberData(nameof(DomainMappings))]
    public async Task Maps_expected_exceptions_to_status_and_keeps_message(Exception ex, int expectedStatus)
    {
        var (status, message, contentType) = await RunAsync(ex);

        Assert.Equal(expectedStatus, status);
        Assert.Equal(ex.Message, message);
        Assert.Equal("application/json", contentType);
    }

    [Fact]
    public async Task Unexpected_exception_returns_500_with_generic_message()
    {
        var (status, message, _) = await RunAsync(new NullReferenceException("secret internals at line 42"));

        Assert.Equal(500, status);
        Assert.Equal(ExceptionToHttpResponseMiddleware.GenericErrorMessage, message);
    }

    /// <summary>Only AuthenticationFailedException means 401; UnauthorizedAccessException is usually an I/O permission error.</summary>
    [Fact]
    public async Task UnauthorizedAccessException_is_not_treated_as_a_login_failure()
    {
        var (status, message, _) = await RunAsync(new UnauthorizedAccessException("Access to the path '/data' is denied."));

        Assert.Equal(500, status);
        Assert.Equal(ExceptionToHttpResponseMiddleware.GenericErrorMessage, message);
    }

    [Fact]
    public async Task InvalidOperationException_falls_back_to_400_without_leaking_its_message()
    {
        var (status, message, _) = await RunAsync(new InvalidOperationException("Sequence contains no elements"));

        Assert.Equal(400, status);
        Assert.NotEqual("Sequence contains no elements", message);
    }

    [Fact]
    public async Task Already_submitted_text_no_longer_implies_conflict()
    {
        var (status, _, _) = await RunAsync(new InvalidOperationException("User has already submitted for this game"));
        Assert.Equal(400, status);

        var (conflict, _, _) = await RunAsync(new ConflictException("User has already submitted for this game"));
        Assert.Equal(409, conflict);
    }

    [Fact]
    public async Task Rethrows_when_response_has_already_started()
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
        var boom = new ConflictException("too late");
        var middleware = new ExceptionToHttpResponseMiddleware(_ => throw boom, NullLogger<ExceptionToHttpResponseMiddleware>.Instance);

        var thrown = await Assert.ThrowsAsync<ConflictException>(() => middleware.InvokeAsync(context));
        Assert.Same(boom, thrown);
    }

    [Fact]
    public async Task Passes_through_when_nothing_throws()
    {
        var context = new DefaultHttpContext();
        var middleware = new ExceptionToHttpResponseMiddleware(ctx => { ctx.Response.StatusCode = 204; return Task.CompletedTask; },
            NullLogger<ExceptionToHttpResponseMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(204, context.Response.StatusCode);
    }

    [Fact]
    public async Task Client_abort_is_swallowed_without_an_error_log_or_body()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };
        context.Response.Body = new MemoryStream();
        var logger = new RecordingLogger();
        var middleware = new ExceptionToHttpResponseMiddleware(_ => throw new TaskCanceledException(), logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(0, context.Response.Body.Length);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal([LogLevel.Debug], logger.Levels);
    }

    [Fact]
    public async Task Client_abort_after_the_response_started_is_not_rethrown()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
        var logger = new RecordingLogger();
        var middleware = new ExceptionToHttpResponseMiddleware(_ => throw new OperationCanceledException(), logger);

        await middleware.InvokeAsync(context);

        Assert.Equal([LogLevel.Debug], logger.Levels);
    }

    [Fact]
    public async Task Cancellation_without_a_client_abort_is_still_an_unexpected_500()
    {
        var (status, message, _) = await RunAsync(new OperationCanceledException("internal timeout"));

        Assert.Equal(500, status);
        Assert.Equal(ExceptionToHttpResponseMiddleware.GenericErrorMessage, message);
    }

    private sealed class RecordingLogger : ILogger<ExceptionToHttpResponseMiddleware>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}
