using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hookivery.Api.Sources;
using Hookivery.Domain.Events;
using Hookivery.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Hookivery.IntegrationTests.Sources;

public sealed class SourceApiTests(SourceApiFactory factory) : IClassFixture<SourceApiFactory>
{
    private const string BaseUrl = "/api/v1/sources";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateReturnsTheSecretOnceAndGetNeverDoes()
    {
        using var response = await _client.PostAsJsonAsync(BaseUrl, new { name = "Billing" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreatedSourceResponse>();
        Assert.NotNull(created);
        Assert.Equal(43, created.Secret.Length);
        Assert.True(created.IsActive);
        Assert.Equal($"{BaseUrl}/{created.Id}", response.Headers.Location?.OriginalString);

        var body = await _client.GetStringAsync(new Uri($"{BaseUrl}/{created.Id}", UriKind.Relative));
        Assert.DoesNotContain(created.Secret, body, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "name": "   " }""")]
    public async Task CreateWithoutANameReturnsValidationProblem(string json)
    {
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        using var response = await _client.PostAsync(new Uri(BaseUrl, UriKind.Relative), content);

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task CreateWithATooLongNameReturnsValidationProblem()
    {
        using var response = await _client.PostAsJsonAsync(BaseUrl, new { name = new string('a', 201) });

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task UpdateWithoutIsActiveReturnsValidationProblem()
    {
        var created = await CreateAsync("Billing");

        using var response = await _client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", new { name = "Billing" });

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task UnknownSourceReturnsProblemDetails404()
    {
        using var response = await _client.GetAsync(new Uri($"{BaseUrl}/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UpdateRenamesAndDeactivates()
    {
        var created = await CreateAsync("Billing");

        using var response = await _client.PutAsJsonAsync(
            $"{BaseUrl}/{created.Id}", new { name = "Billing v2", isActive = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<SourceResponse>();
        Assert.Equal("Billing v2", updated?.Name);
        Assert.False(updated?.IsActive);
    }

    [Fact]
    public async Task DeleteDeactivatesIdempotentlyAndTheDomainRefusesTheSource()
    {
        var created = await CreateAsync("Billing");
        var url = new Uri($"{BaseUrl}/{created.Id}", UriKind.Relative);

        using var first = await _client.DeleteAsync(url);
        using var second = await _client.DeleteAsync(url);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        var stored = await _client.GetFromJsonAsync<SourceResponse>(url);
        Assert.False(stored?.IsActive);

        // TASK-009, criterion 4: the persisted state is what the ingestion rule reads.
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<HookiveryDbContext>();
        var source = await context.Sources.SingleAsync(s => s.Id == created.Id, CancellationToken.None);

        Assert.Throws<InvalidOperationException>(() =>
            Event.Receive(source, "evt-1", "order.created", "{}", DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ListContainsCreatedSourcesAndRejectsAnInvalidLimit()
    {
        var created = await CreateAsync("Billing");

        var page = await _client.GetFromJsonAsync<SourceResponse[]>(
            new Uri($"{BaseUrl}?limit=100", UriKind.Relative));
        Assert.NotNull(page);
        Assert.True(page.Length <= 100);

        var found = await _client.GetFromJsonAsync<SourceResponse>(
            new Uri($"{BaseUrl}/{created.Id}", UriKind.Relative));
        Assert.Equal(created.Id, found?.Id);

        using var invalid = await _client.GetAsync(new Uri($"{BaseUrl}?limit=0", UriKind.Relative));
        await AssertValidationProblemAsync(invalid);
    }

    private async Task<CreatedSourceResponse> CreateAsync(string name)
    {
        using var response = await _client.PostAsJsonAsync(BaseUrl, new { name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedSourceResponse>())!;
    }

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("errors", out _));
    }
}
