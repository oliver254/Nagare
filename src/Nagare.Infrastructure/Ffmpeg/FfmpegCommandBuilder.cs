using System.Globalization;
using Nagare.Application.Abstractions;
using Nagare.Domain.Channels;
using Nagare.Domain.Profiles;

namespace Nagare.Infrastructure.Ffmpeg;

/// <summary>
/// Maps a profile + channel + input file to the ffmpeg arguments in the STRICT canonical
/// order (ARCHITECTURE.md §6.1). Decrypts the key via <see cref="IStreamKeyProtector"/>;
/// the plaintext only appears in <see cref="FfmpegCommand.Arguments"/> and
/// <see cref="FfmpegCommand.Secrets"/> (opaque by convention). The displayable
/// <see cref="FfmpegCommand.MaskedCommandLine"/> replaces the key with ****.
/// </summary>
public sealed class FfmpegCommandBuilder(IStreamKeyProtector keyProtector) : IFfmpegCommandBuilder
{
    public FfmpegCommand Build(StreamProfile profile, Channel channel, string inputFilePath)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(channel);
        if (string.IsNullOrWhiteSpace(inputFilePath))
            throw new ArgumentException("The input file path is required.", nameof(inputFilePath));

        var plaintextKey = keyProtector.Unprotect(channel.Key);
        var destination = BuildDestination(channel.BaseUrl, plaintextKey);
        var maskedDestination = BuildDestination(channel.BaseUrl, ProtectedStreamKey.Mask);

        var prefix = BuildPrefix(profile, inputFilePath);

        var arguments = new List<string>(prefix) { destination };
        var maskedArguments = new List<string>(prefix) { maskedDestination };

        return new FfmpegCommand(
            arguments,
            string.Join(' ', maskedArguments),
            [plaintextKey]);
    }

    /// <summary>Arguments 1-17 (everything but the destination), identical for real and masked lines.</summary>
    private static List<string> BuildPrefix(StreamProfile profile, string inputFilePath)
    {
        var video = profile.Video;
        var audio = profile.Audio;
        var input = profile.Input;
        var args = new List<string>();

        // 1 - -re
        if (input.ReadAtNativeRate)
            args.Add("-re");

        // 2 - -stream_loop -1
        if (input.LoopInfinitely)
        {
            args.Add("-stream_loop");
            args.Add("-1");
        }

        // 3 - -i <inputFilePath>
        args.Add("-i");
        args.Add(inputFilePath);

        // 4 - -c:v <codec>
        args.Add("-c:v");
        args.Add(VideoCodecArg(video.Codec));

        // 5 - -preset <preset>
        args.Add("-preset");
        args.Add(video.Preset);

        // 6 - -rc cbr|vbr (NVENC only; libx264 has no -rc)
        if (video.Codec is VideoCodec.H264Nvenc or VideoCodec.HevcNvenc)
        {
            args.Add("-rc");
            args.Add(RateControlArg(video.RateControl));
        }

        // 7 - -b:v <N>k
        args.Add("-b:v");
        args.Add($"{video.BitrateKbps}k");

        // 8 - -maxrate <N>k
        args.Add("-maxrate");
        args.Add($"{video.MaxrateKbps}k");

        // 9 - -bufsize <N>k
        args.Add("-bufsize");
        args.Add($"{video.BufsizeKbps}k");

        // 10 - -g <N>
        args.Add("-g");
        args.Add(video.GopSize.ToString(CultureInfo.InvariantCulture));

        // 11 - -keyint_min <N>
        args.Add("-keyint_min");
        args.Add(video.KeyintMin.ToString(CultureInfo.InvariantCulture));

        // 12 - -vf scale=<W>:<H> (if resolution present)
        if (video.Resolution is { } resolution)
        {
            args.Add("-vf");
            args.Add($"scale={resolution.Width}:{resolution.Height}");
        }

        // 13 - -r <N> (if fps present)
        if (video.Fps is { } fps)
        {
            args.Add("-r");
            args.Add(fps.ToString(CultureInfo.InvariantCulture));
        }

        // 14 - -c:a aac
        args.Add("-c:a");
        args.Add(AudioCodecArg(audio.Codec));

        // 15 - -b:a <N>k
        args.Add("-b:a");
        args.Add($"{audio.BitrateKbps}k");

        // 16 - -ar <N>
        args.Add("-ar");
        args.Add(audio.SampleRateHz.ToString(CultureInfo.InvariantCulture));

        // 17 - -f flv
        args.Add("-f");
        args.Add("flv");

        return args;
    }

    private static string BuildDestination(string baseUrl, string key)
        => $"{baseUrl.TrimEnd('/')}/{key}";

    private static string VideoCodecArg(VideoCodec codec) => codec switch
    {
        VideoCodec.H264Nvenc => "h264_nvenc",
        VideoCodec.HevcNvenc => "hevc_nvenc",
        VideoCodec.Libx264 => "libx264",
        _ => throw new ArgumentOutOfRangeException(nameof(codec), codec, "Unknown video codec.")
    };

    private static string RateControlArg(RateControl rateControl) => rateControl switch
    {
        RateControl.Cbr => "cbr",
        RateControl.Vbr => "vbr",
        _ => throw new ArgumentOutOfRangeException(nameof(rateControl), rateControl, "Unknown rate control.")
    };

    private static string AudioCodecArg(AudioCodec codec) => codec switch
    {
        AudioCodec.Aac => "aac",
        _ => throw new ArgumentOutOfRangeException(nameof(codec), codec, "Unknown audio codec.")
    };
}
