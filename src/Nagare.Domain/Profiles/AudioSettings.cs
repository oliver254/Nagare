using Nagare.Domain.Common;

namespace Nagare.Domain.Profiles;

public enum AudioCodec
{
    Aac   // extensible later
}

/// <summary>Audio settings of an encoding profile. Immutable value object.</summary>
public sealed record AudioSettings
{
    /// <summary>
    /// Sample rates accepted by the targeted RTMP platforms (ARCHITECTURE.md §2.2). Public so the UI
    /// offers exactly these values rather than restating them — the rule stays validated here.
    /// </summary>
    public static IReadOnlyList<int> AllowedSampleRates { get; } = [44100, 48000];

    public AudioCodec Codec { get; }
    public int BitrateKbps { get; }
    public int SampleRateHz { get; }

    public AudioSettings(AudioCodec codec, int bitrateKbps, int sampleRateHz)
    {
        if (bitrateKbps <= 0)
            throw new DomainException("Audio bitrate must be strictly positive.");

        if (!AllowedSampleRates.Contains(sampleRateHz))
            throw new DomainException("Sample rate must be 44100 or 48000 Hz.");

        Codec = codec;
        BitrateKbps = bitrateKbps;
        SampleRateHz = sampleRateHz;
    }
}
