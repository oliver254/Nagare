namespace Nagare.Presentation.Abstractions;

/// <summary>
/// Picks the video file to broadcast. Implemented in the WinUI layer over <c>FileOpenPicker</c>,
/// which in an UNPACKAGED app requires the WinRT HWND interop to even show up (plan §8, R2).
/// </summary>
public interface IVideoFilePicker
{
    /// <summary>Full path of the chosen file, or null if the user cancelled.</summary>
    Task<string?> PickAsync();
}
