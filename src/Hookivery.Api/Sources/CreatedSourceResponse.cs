namespace Hookivery.Api.Sources;

public sealed record CreatedSourceResponse(
    Guid Id, string Name, bool IsActive, DateTimeOffset CreatedAt, string Secret);
