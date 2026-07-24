using Nagare.ViewModels.Abstractions;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Nagare.WinApp.Services;

/// <summary>
/// Designation of the ffmpeg / ffprobe binaries from the settings screen (ADR-0010).
///
/// <para><b>The unpackaged trap, again.</b> Like <see cref="FilePickerService"/>, this
/// <see cref="FileOpenPicker"/> is a WinRT object with no owner window: in an app without package
/// identity it throws (COMException 0x80070578, "invalid window handle") the moment it is shown.
/// <see cref="InitializeWithWindow"/> hands it the HWND and is MANDATORY before any use.</para>
/// </summary>
public sealed class ExecutableFilePickerService(MainWindowContext window) : IExecutableFilePicker
{
    public async Task<string?> PickAsync()
    {
        var picker = new FileOpenPicker
        {
            // Where a binary is looked for: a drive, not a library.
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            ViewMode = PickerViewMode.List
        };

        // MANDATORY before any use, see the class remarks.
        InitializeWithWindow.Initialize(picker, window.Hwnd);

        // A picker with an EMPTY filter list throws on show; ".exe" is also the only thing worth
        // pointing at here — an extensionless build is designated by typing its path.
        picker.FileTypeFilter.Add(".exe");

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }
}
