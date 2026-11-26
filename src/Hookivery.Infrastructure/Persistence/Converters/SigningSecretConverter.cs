using Hookivery.Domain.Common;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Hookivery.Infrastructure.Persistence.Converters;

internal sealed class SigningSecretConverter() : ValueConverter<SigningSecret, string>(secret => secret.Reveal(), value => SigningSecret.FromRawValue(value))
{
    public const int MaxLength = 256;
}
