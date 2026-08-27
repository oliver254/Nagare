namespace Nagare.ViewModels.Abstractions;

/// <summary>
/// Picks a program to run — the ffmpeg and ffprobe binaries of the settings screen (ADR-0010).
///
/// <para>Separate from <see cref="IVideoFilePicker"/> rather than parameterised by a filter: the two
/// answer different questions ("which video do I broadcast?" / "where is the toolchain?"), and a
/// caller of one has no use for the other. Its WinUI implementation carries the same unpackaged
/// constraint — a <c>FileOpenPicker</c> with no owner window throws the moment it is shown.</para>
/// </summary>
public interface IExecutableFilePicker
{
    /// <summary>Full path of the chosen executable, or null if the user cancelled.</summary>
    Task<string?> PickAsync();
}
