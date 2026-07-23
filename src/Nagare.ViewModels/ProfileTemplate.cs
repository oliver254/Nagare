using Nagare.Domain.Profiles;

namespace Nagare.ViewModels;

/// <summary>
/// A ready-made encoding profile, offered in the editor as a starting point.
///
/// <para>It exists because the editor asks for fifteen decisions, down to a <c>keyint_min</c>, and
/// nobody should have to answer them to put a video on their channel (Hick's law, choice overload).
/// The complexity of ffmpeg does not go away — the application absorbs it (Tesler's law), and the
/// expert keeps every field editable underneath.</para>
///
/// <para>A template holds REAL value objects, not loose numbers: building this list runs the
/// domain's own invariants E1-E8 over every one of them, so a template that would be refused at save
/// time cannot exist. It is a pre-fill of the editor and nothing more — no rule is duplicated here,
/// and the user stays free to change everything before saving.</para>
///
/// <para>Two families on purpose: NVENC needs an NVIDIA encoder the machine may not expose (the
/// start preflight blocks on it), so there is always a libx264 answer next to it.</para>
/// </summary>
public sealed record ProfileTemplate(
    string Name,
    EncodingSettings Video,
    AudioSettings Audio,
    InputOptions Input)
{
    public static IReadOnlyList<ProfileTemplate> All { get; } =
    [
        // GOP = 2 s of frames on every one of them: the keyframe interval the RTMP platforms ask for.
        new("Twitch 1080p60 (NVENC)",
            new EncodingSettings(VideoCodec.H264Nvenc, "p5", RateControl.Cbr, 6000, 6000, 6000, 120, 120, new Resolution(1920, 1080), 60),
            new AudioSettings(AudioCodec.Aac, 160, 48000),
            InputOptions.Default),

        new("Twitch 1080p60 (libx264)",
            new EncodingSettings(VideoCodec.Libx264, "veryfast", RateControl.Cbr, 6000, 6000, 6000, 120, 120, new Resolution(1920, 1080), 60),
            new AudioSettings(AudioCodec.Aac, 160, 48000),
            InputOptions.Default),

        new("YouTube 1440p60 (NVENC)",
            new EncodingSettings(VideoCodec.H264Nvenc, "p5", RateControl.Cbr, 12000, 12000, 12000, 120, 120, new Resolution(2560, 1440), 60),
            new AudioSettings(AudioCodec.Aac, 192, 48000),
            InputOptions.Default),

        new("Léger 720p30 (libx264)",
            new EncodingSettings(VideoCodec.Libx264, "veryfast", RateControl.Cbr, 3000, 3000, 3000, 60, 60, new Resolution(1280, 720), 30),
            new AudioSettings(AudioCodec.Aac, 128, 48000),
            InputOptions.Default)
    ];
}
