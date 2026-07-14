using Nagare.ViewModels.Abstractions;

namespace Nagare.WinApp.Services;

/// <summary>
/// The one bridge between the background threads (coordinator mailbox loop, ffmpeg stderr reader)
/// and the UI thread (ADR-0006). Everything the ViewModels push to the screen goes through here.
/// </summary>
public sealed class UiDispatcher(MainWindowContext window) : IUiDispatcher
{
    public bool Post(Action action) => window.Dispatcher.TryEnqueue(() => action());
}
