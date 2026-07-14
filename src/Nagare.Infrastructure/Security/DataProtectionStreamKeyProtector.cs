using Microsoft.AspNetCore.DataProtection;
using Nagare.Application.Abstractions;
using Nagare.Domain.Channels;

namespace Nagare.Infrastructure.Security;

/// <summary>
/// Stream key protection via ASP.NET Core Data Protection (ADR-0005), purpose
/// "Nagare.StreamKey.v1". The keyring is persisted and DPAPI-protected (wired in DI). The
/// plaintext only crosses this boundary: in via <see cref="Protect"/> (SaveChannel handler),
/// out via <see cref="Unprotect"/> (FfmpegCommandBuilder).
/// </summary>
public sealed class DataProtectionStreamKeyProtector : IStreamKeyProtector
{
    public const string Purpose = "Nagare.StreamKey.v1";

    private readonly IDataProtector _protector;

    public DataProtectionStreamKeyProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector(Purpose);

    public ProtectedStreamKey Protect(string plaintextKey)
    {
        if (string.IsNullOrWhiteSpace(plaintextKey))
            throw new ArgumentException("The plaintext stream key cannot be empty.", nameof(plaintextKey));

        return new ProtectedStreamKey(_protector.Protect(plaintextKey));
    }

    public string Unprotect(ProtectedStreamKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _protector.Unprotect(key.CipherText);
    }
}
