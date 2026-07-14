namespace Nagare.Presentation.Abstractions;

/// <summary>
/// Marshals work onto the UI thread. Implemented over <c>DispatcherQueue.TryEnqueue</c> in the
/// WinUI layer (ADR-0006).
///
/// This abstraction exists for ONE reason: <see cref="Nagare.Application.Abstractions.ISessionMonitor"/>
/// raises its events from the coordinator's mailbox loop and from the ffmpeg stderr reader thread —
/// never from the UI thread. Touching an <c>ObservableCollection</c> or a bound property from there
/// corrupts the visual tree or throws. Every subscriber goes through here.
///
/// It also makes the ViewModels testable with no UI at all: a fake that runs the action inline.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// Queues <paramref name="action"/> on the UI thread. Returns false when the UI thread is gone
    /// (window closed) — a caller may then simply drop the update.
    /// </summary>
    bool Post(Action action);
}
