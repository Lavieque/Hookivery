using System.Text.Json;

using Hookivery.Api.ErrorHandling;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Hookivery.IntegrationTests.ErrorHandling;

public sealed class GlobalExceptionHandlerTests : IAsyncLifetime
{
    private readonly ServiceProvider _services = new ServiceCollection()
        .AddLogging()
        .AddProblemDetails()
        .BuildServiceProvider();

    private readonly FakeLogger<GlobalExceptionHandler> _logger = new();

    [Fact]
    public async Task UnexpectedExceptionReturns500WithoutExceptionDetails()
    {
        var context = CreateContext();
        var exception = new InvalidOperationException("Host=db;Password=super-secret");

        var handled = await CreateHandler().TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType, StringComparison.Ordinal);

        var body = await ReadBodyAsync(context);
        Assert.DoesNotContain("super-secret", body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), body, StringComparison.Ordinal);

        using var json = JsonDocument.Parse(body);
        Assert.Equal("An unexpected error occurred.", json.RootElement.GetProperty("title").GetString());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task UnexpectedExceptionIsLoggedAsError()
    {
        var exception = new InvalidOperationException("boom");

        await CreateHandler().TryHandleAsync(CreateContext(), exception, CancellationToken.None);

        Assert.Equal(LogLevel.Error, _logger.LatestRecord.Level);
        Assert.Same(exception, _logger.LatestRecord.Exception);
    }

    [Fact]
    public async Task BadHttpRequestReturns400AndLogsWarning()
    {
        var context = CreateContext();
        var exception = new BadHttpRequestException("Malformed body", StatusCodes.Status400BadRequest);

        await CreateHandler().TryHandleAsync(context, exception, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(LogLevel.Warning, _logger.LatestRecord.Level);
    }

    [Fact]
    public async Task AbortedRequestReturns499WithoutBodyAndWithoutError()
    {
        var context = CreateContext();
        context.RequestAborted = new CancellationToken(canceled: true);

        var handled = await CreateHandler()
            .TryHandleAsync(context, new OperationCanceledException(), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status499ClientClosedRequest, context.Response.StatusCode);
        Assert.Empty(await ReadBodyAsync(context));
        Assert.Equal(LogLevel.Information, _logger.LatestRecord.Level);
    }

    private GlobalExceptionHandler CreateHandler() =>
        new(_services.GetRequiredService<IProblemDetailsService>(), _logger);

    private DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext { RequestServices = _services };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/v1/sources";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _services.DisposeAsync();
}
