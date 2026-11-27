using System.ComponentModel.DataAnnotations;

using Hookivery.Domain.Sources;

namespace Hookivery.Api.Sources;

public sealed record CreateSourceRequest(
    [Required, MaxLength(Source.MaxNameLength)] string Name);