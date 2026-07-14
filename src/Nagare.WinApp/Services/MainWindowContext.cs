using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Nagare.WinApp.Services;

/// <summary>
/// Gives the UI-bound services access to the main window — its dispatcher queue and its HWND.
///
/// It exists because of an ordering problem: the DI container is built BEFORE the window, so
/// nothing can capture a <see cref="DispatcherQueue"/> or a window handle at registration time.
/// The window attaches itself here as soon as it is created; the services resolve lazily.
/// </summary>
public sealed class MainWindowContext
{
    private Window? _window;

    public void Attach(Window window) => _window = window;

    public Window Window => _window
        ?? throw new InvalidOperationException("The main window is not created yet.");

    /// <summary>The UI thread's queue: the only legal way in for a background thread (ADR-0006).</summary>
    public DispatcherQueue Dispatcher => Window.DispatcherQueue;

    /// <summary>
    /// Window handle. An UNPACKAGED app must hand it to every WinRT picker/dialog, which otherwise
    /// has no way to find its owner window and crashes (plan §8, R2).
    /// </summary>
    public nint Hwnd => WindowNative.GetWindowHandle(Window);
}
