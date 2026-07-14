namespace Nagare.Application.Abstractions;

/// <summary>
/// Creates a fresh <see cref="IFfmpegProcessRunner"/> per launch/relaunch. A runner
/// wraps a single ffmpeg process; the coordinator disposes it before spinning up a new
/// one on reconnection.
/// </summary>
public interface IFfmpegProcessRunnerFactory
{
    IFfmpegProcessRunner Create();
}
