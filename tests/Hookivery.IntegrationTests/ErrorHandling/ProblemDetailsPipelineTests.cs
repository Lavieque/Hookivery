using System.Net;
using System.Text.Json;

namespace Hookivery.IntegrationTests.ErrorHandling;

public sealed class ProblemDetailsPipelineTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task UnknownRouteReturnsProblemDetails()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/does-not-exist", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("/does-not-exist", json.RootElement.GetProperty("instance").GetString());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
    }
}
