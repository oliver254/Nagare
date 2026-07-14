using Nagare.Domain.Common;
using Nagare.Domain.Profiles;

namespace Nagare.UnitTests.Domain.Profiles;

/// <summary>Invariants of the audio value object (ARCHITECTURE.md §2.2).</summary>
public sealed class AudioSettingsTests
{
    [Theory]
    [InlineData(128, 48000)]   // spec profile
    [InlineData(160, 44100)]
    public void Constructor_SupportedSampleRate_IsAccepted(int bitrateKbps, int sampleRateHz)
    {
        var settings = new AudioSettings(AudioCodec.Aac, bitrateKbps, sampleRateHz);

        Assert.Equal(AudioCodec.Aac, settings.Codec);
        Assert.Equal(bitrateKbps, settings.BitrateKbps);
        Assert.Equal(sampleRateHz, settings.SampleRateHz);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-128)]
    public void Constructor_NonPositiveBitrate_ThrowsDomainException(int bitrateKbps)
        => Assert.Throws<DomainException>(() => new AudioSettings(AudioCodec.Aac, bitrateKbps, 48000));

    [Theory]
    [InlineData(22050)]
    [InlineData(32000)]
    [InlineData(96000)]
    [InlineData(0)]
    public void Constructor_SampleRateRejectedByRtmpPlatforms_ThrowsDomainException(int sampleRateHz)
        => Assert.Throws<DomainException>(() => new AudioSettings(AudioCodec.Aac, 128, sampleRateHz));
}
