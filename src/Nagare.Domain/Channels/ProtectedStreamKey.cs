using Nagare.Domain.Common;

namespace Nagare.Domain.Channels;

/// <summary>
/// Stream key in encrypted form, an opaque value (ADR-0005). The plaintext never
/// exists in Domain nor Application: Protect/Unprotect live behind the
/// IStreamKeyProtector port, implemented in Infrastructure.
/// </summary>
public sealed record ProtectedStreamKey
{
    public const string Mask = "****";

    /// <summary>Data Protection payload, base64.</summary>
    public string CipherText { get; }

    public ProtectedStreamKey(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
            throw new DomainException("The protected stream key cannot be empty.");
        CipherText = cipherText;
    }

    /// <summary>Pit of success: an accidental log or interpolation shows ****.</summary>
    public override string ToString() => Mask;
}
