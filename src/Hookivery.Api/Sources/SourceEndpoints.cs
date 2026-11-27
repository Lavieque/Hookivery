using System.ComponentModel.DataAnnotations;

using Hookivery.Application.Sources;

using Microsoft.AspNetCore.Http.HttpResults;

namespace Hookivery.Api.Sources;

internal static class SourceEndpoints
{
    public static IEndpointRouteBuilder MapSourceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sources").WithTags("Sources");

        group.MapPost("", CreateAsync);
        group.MapGet("", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeactivateAsync);

        return app;
    }

    private static async Task<Created<CreatedSourceResponse>> CreateAsync(
        CreateSourceRequest request, SourceService service, CancellationToken cancellationToken)
    {
        var source = await service.RegisterAsync(request.Name, cancellationToken);

        var body = new CreatedSourceResponse(
            source.Id, source.Name, source.IsActive, source.CreatedAt, source.Secret.Reveal());

        return TypedResults.Created($"/api/v1/sources/{source.Id}", body);
    }

    private static async Task<Ok<SourceResponse[]>> ListAsync(
        SourceService service,
        CancellationToken cancellationToken,
        [Range(0, int.MaxValue)] int offset = 0,
        [Range(1, SourceService.MaxPageSize)] int limit = 50)
    {
        var page = await service.ListAsync(offset, limit, cancellationToken);
        return TypedResults.Ok(page.Select(SourceResponse.From).ToArray());
    }

    private static async Task<Results<Ok<SourceResponse>, NotFound>> GetAsync(
        Guid id, SourceService service, CancellationToken cancellationToken) =>
        await service.GetAsync(id, cancellationToken) is { } source
            ? TypedResults.Ok(SourceResponse.From(source))
            : TypedResults.NotFound();

    private static async Task<Results<Ok<SourceResponse>, NotFound>> UpdateAsync(
        Guid id, UpdateSourceRequest request, SourceService service, CancellationToken cancellationToken) =>
        await service.UpdateAsync(id, request.Name, request.IsActive!.Value, cancellationToken) is { } source
            ? TypedResults.Ok(SourceResponse.From(source))
            : TypedResults.NotFound();

    private static async Task<Results<NoContent, NotFound>> DeactivateAsync(
        Guid id, SourceService service, CancellationToken cancellationToken) =>
        await service.DeactivateAsync(id, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
}
