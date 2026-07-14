namespace Nagare.Domain.Profiles;

/// <summary>
/// ffmpeg input options: <see cref="ReadAtNativeRate"/> -> -re,
/// <see cref="LoopInfinitely"/> -> -stream_loop -1.
/// </summary>
public sealed record InputOptions(bool ReadAtNativeRate, bool LoopInfinitely)
{
    /// <summary>Business default: read at native rate + infinite loop.</summary>
    public static InputOptions Default => new(ReadAtNativeRate: true, LoopInfinitely: true);
}
