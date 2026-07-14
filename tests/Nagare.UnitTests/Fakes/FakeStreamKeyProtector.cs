using Nagare.Application.Abstractions;
using Nagare.Domain.Channels;

namespace Nagare.UnitTests.Fakes;

/// <summary>
/// Manual fake of <see cref="IStreamKeyProtector"/> (no mock framework). The cipher text
/// is deliberately different from the plaintext so a test can prove the builder really
/// decrypts the key instead of echoing <see cref="ProtectedStreamKey.CipherText"/>.
/// </summary>
public sealed class FakeStreamKeyProtector : IStreamKeyProtector
{
    private const string Prefix = "cipher:";

    public int UnprotectCallCount { get; private set; }

    public ProtectedStreamKey Protect(string plaintextKey) => new(Prefix + plaintextKey);

    public string Unprotect(ProtectedStreamKey key)
    {
        UnprotectCallCount++;

        return key.CipherText.StartsWith(Prefix, StringComparison.Ordinal)
            ? key.CipherText[Prefix.Length..]
            : throw new InvalidOperationException($"Cipher text not produced by {nameof(FakeStreamKeyProtector)}.");
    }
}
