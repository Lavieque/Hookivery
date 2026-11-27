using System.Buffers.Text;
using System.Security.Cryptography;

using Hookivery.Domain.Common;

namespace Hookivery.Application.Sources;

internal static class SigningSecretGenerator
{
    private const int ByteLength = 32;

    public static SigningSecret Generate() =>
        SigningSecret.FromRawValue(Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(ByteLength)));
}
