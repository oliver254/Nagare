using Nagare.Domain.Channels;
using Nagare.Domain.Common;

namespace Nagare.UnitTests.Domain.Channels;

/// <summary>
/// The stream key only ever exists encrypted in the Domain (ADR-0005); ToString() is the
/// "pit of success": an accidental log or interpolation shows **** and never the payload.
/// </summary>
public sealed class ProtectedStreamKeyTests
{
    [Fact]
    public void ToString_AnyKey_ReturnsTheMask()
    {
        var key = new ProtectedStreamKey("Q2lwaGVyVGV4dFBheWxvYWQ=");

        Assert.Equal("****", key.ToString());
        Assert.Equal(ProtectedStreamKey.Mask, key.ToString());
    }

    [Fact]
    public void ToString_UsedInStringInterpolation_NeverLeaksTheCipherText()
    {
        var key = new ProtectedStreamKey("Q2lwaGVyVGV4dFBheWxvYWQ=");

        var logLine = $"channel key = {key}";

        Assert.Equal("channel key = ****", logLine);
        Assert.DoesNotContain("Q2lwaGVyVGV4dFBheWxvYWQ=", logLine, StringComparison.Ordinal);
    }

    [Fact]
    public void CipherText_ValidPayload_IsPreservedForPersistence()
    {
        var key = new ProtectedStreamKey("Q2lwaGVyVGV4dFBheWxvYWQ=");

        Assert.Equal("Q2lwaGVyVGV4dFBheWxvYWQ=", key.CipherText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_BlankCipherText_ThrowsDomainException(string? cipherText)
        => Assert.Throws<DomainException>(() => new ProtectedStreamKey(cipherText!));
}
