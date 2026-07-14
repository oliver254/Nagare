using Nagare.Domain.Common;
using Nagare.Domain.Profiles;

namespace Nagare.UnitTests.Domain.Profiles;

/// <summary>Invariants E1-E8 of the video value object (ARCHITECTURE.md §2.2).</summary>
public sealed class EncodingSettingsTests
{
    // ------------------------------------------------------------- valid settings

    [Fact]
    public void Constructor_SpecCbrNvencSettings_KeepsEveryValue()
    {
        var settings = new EncodingSettings(
            VideoCodec.H264Nvenc, "p2", RateControl.Cbr,
            bitrateKbps: 3000, maxrateKbps: 3000, bufsizeKbps: 3000,
            gopSize: 60, keyintMin: 60, resolution: null, fps: null);

        Assert.Equal(VideoCodec.H264Nvenc, settings.Codec);
        Assert.Equal("p2", settings.Preset);
        Assert.Equal(RateControl.Cbr, settings.RateControl);
        Assert.Equal(3000, settings.BitrateKbps);
        Assert.Equal(3000, settings.MaxrateKbps);
        Assert.Equal(3000, settings.BufsizeKbps);
        Assert.Equal(60, settings.GopSize);
        Assert.Equal(60, settings.KeyintMin);
        Assert.Null(settings.Resolution);
        Assert.Null(settings.Fps);
    }

    [Fact]
    public void Constructor_VbrWithHigherMaxrateAndOptionalFields_IsAccepted()
    {
        var settings = new EncodingSettings(
            VideoCodec.Libx264, "veryfast", RateControl.Vbr,
            bitrateKbps: 4500, maxrateKbps: 6000, bufsizeKbps: 9000,
            gopSize: 120, keyintMin: 60,
            resolution: new Resolution(1920, 1080), fps: 60);

        Assert.Equal(new Resolution(1920, 1080), settings.Resolution);
        Assert.Equal(60, settings.Fps);
    }

    // ---------------------------------------------------- E1: strictly positive rates

    [Theory]
    [InlineData(0, 3000, 3000)]
    [InlineData(-1, 3000, 3000)]
    [InlineData(3000, 0, 3000)]
    [InlineData(3000, -1, 3000)]
    [InlineData(3000, 3000, 0)]
    [InlineData(3000, 3000, -1)]
    public void Constructor_NonPositiveRate_ThrowsDomainException(int bitrate, int maxrate, int bufsize)
        => AssertRejected(RateControl.Vbr, bitrate, maxrate, bufsize, gopSize: 60, keyintMin: 60);

    // ----------------------------------------------- E2/E3: rate control consistency

    [Theory]
    [InlineData(3000, 6000)]   // CBR with a higher maxrate is not CBR
    [InlineData(3000, 2000)]   // CBR with a lower maxrate either
    public void Constructor_CbrWithMaxrateDifferentFromBitrate_ThrowsDomainException(int bitrate, int maxrate)
        => AssertRejected(RateControl.Cbr, bitrate, maxrate, bufsize: 12000, gopSize: 60, keyintMin: 60);

    [Fact]
    public void Constructor_VbrWithMaxrateBelowBitrate_ThrowsDomainException()
        => AssertRejected(RateControl.Vbr, bitrate: 6000, maxrate: 3000, bufsize: 12000, gopSize: 60, keyintMin: 60);

    [Fact]
    public void Constructor_VbrWithMaxrateEqualToBitrate_IsAccepted()
    {
        var settings = Build(RateControl.Vbr, bitrate: 3000, maxrate: 3000, bufsize: 3000, gopSize: 60, keyintMin: 60);

        Assert.Equal(3000, settings.MaxrateKbps);
    }

    // ------------------------------------------------------- E4: bufsize >= bitrate

    [Fact]
    public void Constructor_BufsizeBelowBitrate_ThrowsDomainException()
        => AssertRejected(RateControl.Cbr, bitrate: 3000, maxrate: 3000, bufsize: 2999, gopSize: 60, keyintMin: 60);

    [Fact]
    public void Constructor_BufsizeEqualToBitrate_IsAccepted()
    {
        // The boundary is strict: bufsize == bitrate is the spec profile itself.
        var settings = Build(RateControl.Cbr, bitrate: 3000, maxrate: 3000, bufsize: 3000, gopSize: 60, keyintMin: 60);

        Assert.Equal(3000, settings.BufsizeKbps);
    }

    // -------------------------------------------------------------- E5: GOP / keyint

    [Theory]
    [InlineData(0, 60)]     // g <= 0
    [InlineData(-60, 60)]
    [InlineData(60, 0)]     // keyint_min <= 0
    [InlineData(60, -1)]
    [InlineData(60, 61)]    // keyint_min > g
    public void Constructor_IncoherentGop_ThrowsDomainException(int gopSize, int keyintMin)
        => AssertRejected(RateControl.Cbr, bitrate: 3000, maxrate: 3000, bufsize: 3000, gopSize, keyintMin);

    [Fact]
    public void Constructor_KeyintMinEqualToGop_IsAccepted()
    {
        var settings = Build(RateControl.Cbr, bitrate: 3000, maxrate: 3000, bufsize: 3000, gopSize: 60, keyintMin: 60);

        Assert.Equal(60, settings.KeyintMin);
    }

    // ------------------------------------------------------- E6: preset of the codec

    [Theory]
    [InlineData(VideoCodec.H264Nvenc, "p1")]
    [InlineData(VideoCodec.H264Nvenc, "p7")]
    [InlineData(VideoCodec.HevcNvenc, "p4")]
    [InlineData(VideoCodec.Libx264, "ultrafast")]
    [InlineData(VideoCodec.Libx264, "veryslow")]
    public void Constructor_PresetKnownForTheCodec_IsAccepted(VideoCodec codec, string preset)
    {
        var settings = new EncodingSettings(
            codec, preset, RateControl.Cbr, 3000, 3000, 3000, 60, 60, null, null);

        Assert.Equal(preset, settings.Preset);
    }

    [Theory]
    [InlineData(VideoCodec.H264Nvenc, "p0")]
    [InlineData(VideoCodec.H264Nvenc, "p8")]
    [InlineData(VideoCodec.H264Nvenc, "veryfast")]   // libx264 preset on NVENC
    [InlineData(VideoCodec.HevcNvenc, "medium")]
    [InlineData(VideoCodec.Libx264, "p2")]           // NVENC preset on libx264
    [InlineData(VideoCodec.Libx264, "")]
    [InlineData(VideoCodec.H264Nvenc, "P2")]         // preset matching is case-sensitive, like ffmpeg
    public void Constructor_PresetUnknownForTheCodec_ThrowsDomainException(VideoCodec codec, string preset)
        => Assert.Throws<DomainException>(() => new EncodingSettings(
            codec, preset, RateControl.Cbr, 3000, 3000, 3000, 60, 60, null, null));

    // --------------------------------------------------------- E7: even resolution

    [Theory]
    [InlineData(1281, 720)]
    [InlineData(1280, 721)]
    [InlineData(0, 720)]
    [InlineData(1280, 0)]
    [InlineData(-1280, 720)]
    public void Constructor_InvalidResolution_ThrowsDomainException(int width, int height)
        => Assert.Throws<DomainException>(() => new EncodingSettings(
            VideoCodec.H264Nvenc, "p2", RateControl.Cbr, 3000, 3000, 3000, 60, 60,
            new Resolution(width, height), null));

    [Fact]
    public void Constructor_EvenResolution_IsAccepted()
    {
        var settings = new EncodingSettings(
            VideoCodec.H264Nvenc, "p2", RateControl.Cbr, 3000, 3000, 3000, 60, 60,
            new Resolution(1280, 720), null);

        Assert.Equal(new Resolution(1280, 720), settings.Resolution);
    }

    // -------------------------------------------------------------------- E8: fps

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void Constructor_NonPositiveFps_ThrowsDomainException(int fps)
        => Assert.Throws<DomainException>(() => new EncodingSettings(
            VideoCodec.H264Nvenc, "p2", RateControl.Cbr, 3000, 3000, 3000, 60, 60, null, fps));

    // ---------------------------------------------------------------------- helpers

    private static EncodingSettings Build(
        RateControl rateControl, int bitrate, int maxrate, int bufsize, int gopSize, int keyintMin)
        => new(VideoCodec.H264Nvenc, "p2", rateControl, bitrate, maxrate, bufsize, gopSize, keyintMin, null, null);

    private static void AssertRejected(
        RateControl rateControl, int bitrate, int maxrate, int bufsize, int gopSize, int keyintMin)
        => Assert.Throws<DomainException>(() => Build(rateControl, bitrate, maxrate, bufsize, gopSize, keyintMin));
}
