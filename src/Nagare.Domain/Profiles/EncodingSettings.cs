using Nagare.Domain.Common;

namespace Nagare.Domain.Profiles;

public enum VideoCodec
{
    H264Nvenc,   // -> h264_nvenc
    HevcNvenc,   // -> hevc_nvenc
    Libx264      // -> libx264
}

public enum RateControl { Cbr, Vbr }

public readonly record struct Resolution(int Width, int Height);

/// <summary>
/// Video settings of an encoding profile. Immutable value object, invariants
/// E1-E8 validated in the constructor (ARCHITECTURE.md §2.2).
/// </summary>
public sealed record EncodingSettings
{
    private static readonly string[] NvencPresets = ["p1", "p2", "p3", "p4", "p5", "p6", "p7"];

    private static readonly string[] Libx264Presets =
        ["ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"];

    public VideoCodec Codec { get; }
    public string Preset { get; }
    public RateControl RateControl { get; }
    public int BitrateKbps { get; }
    public int MaxrateKbps { get; }
    public int BufsizeKbps { get; }

    /// <summary>-g</summary>
    public int GopSize { get; }

    /// <summary>-keyint_min</summary>
    public int KeyintMin { get; }

    /// <summary>Optional -> -vf scale=W:H</summary>
    public Resolution? Resolution { get; }

    /// <summary>Optional -> -r</summary>
    public int? Fps { get; }

    /// <summary>
    /// Presets accepted by a codec — the very list invariant E6 validates against. Public so the UI
    /// can OFFER exactly the valid values instead of restating them: a second copy of this list in a
    /// ComboBox would be a duplicate of a domain rule, free to drift away from it.
    /// </summary>
    public static IReadOnlyList<string> PresetsFor(VideoCodec codec)
        => codec == VideoCodec.Libx264 ? Libx264Presets : NvencPresets;

    public EncodingSettings(
        VideoCodec codec,
        string preset,
        RateControl rateControl,
        int bitrateKbps,
        int maxrateKbps,
        int bufsizeKbps,
        int gopSize,
        int keyintMin,
        Resolution? resolution,
        int? fps)
    {
        // E1 - valid ffmpeg values
        if (bitrateKbps <= 0 || maxrateKbps <= 0 || bufsizeKbps <= 0)
            throw new DomainException("E1: bitrate, maxrate and bufsize must be strictly positive.");

        // E2 - CBR definition
        if (rateControl == RateControl.Cbr && maxrateKbps != bitrateKbps)
            throw new DomainException("E2: in CBR, maxrate must equal bitrate.");

        // E3 - coherent ceiling in VBR
        if (rateControl == RateControl.Vbr && maxrateKbps < bitrateKbps)
            throw new DomainException("E3: in VBR, maxrate must be greater than or equal to bitrate.");

        // E4 - coherent VBV buffer for live RTMP (strict)
        if (bufsizeKbps < bitrateKbps)
            throw new DomainException("E4: bufsize must be greater than or equal to bitrate.");

        // E5 - coherent GOP (otherwise ffmpeg clamps silently)
        if (gopSize <= 0 || keyintMin <= 0 || keyintMin > gopSize)
            throw new DomainException("E5: GOP must be positive and 0 < keyint_min <= g.");

        // E6 - preset known for the codec
        if (!PresetsFor(codec).Contains(preset))
            throw new DomainException($"E6: preset '{preset}' unknown for codec {codec}.");

        // E7 - h264/hevc encoder requirement: even dimensions
        if (resolution is { } r && (r.Width <= 0 || r.Height <= 0 || r.Width % 2 != 0 || r.Height % 2 != 0))
            throw new DomainException("E7: resolution must be strictly positive and even in both width and height.");

        // E8 - valid fps
        if (fps is <= 0)
            throw new DomainException("E8: fps must be strictly positive.");

        Codec = codec;
        Preset = preset;
        RateControl = rateControl;
        BitrateKbps = bitrateKbps;
        MaxrateKbps = maxrateKbps;
        BufsizeKbps = bufsizeKbps;
        GopSize = gopSize;
        KeyintMin = keyintMin;
        Resolution = resolution;
        Fps = fps;
    }
}
