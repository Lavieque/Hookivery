using Hookivery.Domain.Sources;

namespace Hookivery.Api.Sources;

public sealed record SourceResponse(Guid Id, string Name, bool IsActive, DateTimeOffset CreatedAt)
{
    public static SourceResponse From(Source source) =>
        new(source.Id, source.Name, source.IsActive, source.CreatedAt);
}
