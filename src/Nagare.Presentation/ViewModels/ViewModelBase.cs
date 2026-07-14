using CommunityToolkit.Mvvm.ComponentModel;
using Nagare.Domain.Common;

namespace Nagare.Presentation.ViewModels;

/// <summary>
/// Shared error surface of the pages: an <c>InfoBar</c> fed by <see cref="ErrorMessage"/>
/// (plan §6).
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>Message shown in the page's InfoBar. Null = no error.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Runs a command body and turns its failure into a message instead of a crash.
    ///
    /// The broad catch is deliberate and belongs HERE, at the command boundary: an
    /// <c>AsyncRelayCommand</c> rethrows on the UI synchronization context, so any escaping
    /// exception — a locked repository file, ffprobe vanishing mid-call — takes the whole window
    /// down. A ViewModel is the last place that can still say something to the user.
    ///
    /// <see cref="DomainException"/> is separated out because it is NOT a failure: it is the domain
    /// stating a rule (E1-E8, invariants of Channel), and its text is the message to show. The UI
    /// never restates those rules — the domain remains the single source of truth.
    /// </summary>
    protected async Task RunGuardedAsync(Func<Task> action)
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            await action();
        }
        catch (DomainException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur inattendue : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
