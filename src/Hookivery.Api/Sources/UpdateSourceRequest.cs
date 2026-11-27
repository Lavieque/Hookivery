using System.ComponentModel.DataAnnotations;

using Hookivery.Domain.Sources;

namespace Hookivery.Api.Sources;

public sealed record UpdateSourceRequest(
    [Required, MaxLength(Source.MaxNameLength)] string Name,
    [Required] bool? IsActive);

