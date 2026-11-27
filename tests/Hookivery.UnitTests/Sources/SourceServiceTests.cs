using Hookivery.Application.Sources;
using Hookivery.Domain.Common;
using Hookivery.Domain.Sources;

using Microsoft.Extensions.Logging.Abstractions;

namespace Hookivery.UnitTests.Sources;

public sealed class SourceServiceTests
{
    private readonly InMemorySourceRepository _repository = new();

    private SourceService CreateService() =>
        new(_repository, new FixedTimeProvider(TestData.Now), NullLogger<SourceService>.Instance);

    [Fact]
    public async Task RegisterGeneratesADistinctSecretPerSource()
    {
        var service = CreateService();

        var first = await service.RegisterAsync("Billing", CancellationToken.None);
        var second = await service.RegisterAsync("Shipping", CancellationToken.None);

        Assert.NotEqual(first.Secret, second.Secret);
        Assert.True(first.Secret.Reveal().Length >= SigningSecret.MinLength);
        Assert.Equal(TestData.Now, first.CreatedAt);
        Assert.Equal(2, _repository.Count);
    }

    [Fact]
    public async Task DeactivateIsIdempotent()
    {
        var service = CreateService();
        var source = await service.RegisterAsync("Billing", CancellationToken.None);

        Assert.True(await service.DeactivateAsync(source.Id, CancellationToken.None));
        Assert.True(await service.DeactivateAsync(source.Id, CancellationToken.None));
        Assert.False(source.IsActive);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task UnknownSourceIsReportedNotThrown()
    {
        var service = CreateService();

        Assert.Null(await service.UpdateAsync(Guid.NewGuid(), "x", isActive: true, CancellationToken.None));
        Assert.False(await service.DeactivateAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateRenamesAndReactivates()
    {
        var service = CreateService();
        var source = await service.RegisterAsync("Billing", CancellationToken.None);
        await service.DeactivateAsync(source.Id, CancellationToken.None);

        var updated = await service.UpdateAsync(source.Id, "  Billing v2 ", isActive: true, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Billing v2", updated.Name);
        Assert.True(updated.IsActive);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InMemorySourceRepository : ISourceRepository
    {
        private readonly Dictionary<Guid, Source> _sources = [];

        public int Count => _sources.Count;

        public int SaveCount { get; private set; }

        public Task AddAsync(Source source, CancellationToken cancellationToken)
        {
            _sources.Add(source.Id, source);
            return Task.CompletedTask;
        }

        public Task<Source?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_sources.GetValueOrDefault(id));

        public Task<IReadOnlyList<Source>> ListAsync(int offset, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Source>>(
                [.. _sources.Values.OrderBy(s => s.Id).Skip(offset).Take(limit)]);

        public Task SaveAsync(Source source, CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}

