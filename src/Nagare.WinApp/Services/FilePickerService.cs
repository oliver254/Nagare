using Nagare.Presentation.Abstractions;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Nagare.WinApp.Services;

/// <summary>
/// Video file selection (SPEC §1).
///
/// <para><b>The unpackaged trap (plan §8, R2).</b> A <see cref="FileOpenPicker"/> is a WinRT object:
/// it needs an owner window. In a PACKAGED app the identity of the app supplies one; here there is
/// none, and the picker throws (COMException 0x80070578, "invalid window handle") the moment it is
/// shown. <see cref="InitializeWithWindow"/> is what hands it the HWND — five lines that are the
/// difference between a working file dialog and a crash.</para>
/// </summary>
public sealed class FilePickerService(MainWindowContext window) : IVideoFilePicker
{
    private static readonly string[] VideoExtensions = [".mp4", ".mkv", ".mov", ".flv", ".avi", ".ts", ".webm"];

    public async Task<string?> PickAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.VideosLibrary,
            ViewMode = PickerViewMode.List
        };

        // MANDATORY before any use, see the class remarks.
        InitializeWithWindow.Initialize(picker, window.Hwnd);

        foreach (var extension in VideoExtensions)
            picker.FileTypeFilter.Add(extension);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }
}
