using Nagare.Domain.Channels;

namespace Nagare.Application.Abstractions;

/// <summary>
/// Stream key protection port (ADR-0005). <see cref="Protect"/> is called once, in the
/// SaveChannelCommand handler. <see cref="Unprotect"/> is called ONLY by the
/// Infrastructure (FfmpegCommandBuilder), when building the command.
/// </summary>
public interface IStreamKeyProtector
{
    ProtectedStreamKey Protect(string plaintextKey);
    string Unprotect(ProtectedStreamKey key);
}
