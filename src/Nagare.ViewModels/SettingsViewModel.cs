using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Monbsoft.BrilliantMediator.Abstractions;
using Nagare.Application.Abstractions;
using Nagare.Application.Settings;
using Nagare.ViewModels.Abstractions;

namespace Nagare.ViewModels;

/// <summary>
/// The settings screen: where the ffmpeg and ffprobe paths are detected, tested and saved
/// (ADR-0010). It is the ONLY writer of those paths, and the answer the dashboard points at when it
/// reports a missing binary.
///
/// NO RULE LIVES HERE. Whether a pair of paths runs is answered by
/// <see cref="ValidateFfmpegPathsQuery"/>, and whether it may be persisted by
/// <see cref="SaveFfmpegSettingsCommand"/> — which refuses to write a configuration that does not
/// work, so the application cannot be locked out of its own toolchain by a typo. This class does
/// two things with those answers: it pours them into the fields, and it TRANSLATES them into the
/// French sentence shown on screen, exactly as <see cref="DashboardViewModel"/> translates a
/// <c>StartBlockReason</c>.
///
/// <para><b>An empty field is a legitimate value</b> — it means "resolve the binary from the PATH"
/// — so nothing here refuses one. Trimming is not done either: the handler normalises the input,
/// and doing it twice would let the two disagree the day one of them changes.</para>
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly IMediator _mediator;
    private readonly IExecutableFilePicker _picker;

    public SettingsViewModel(IMediator mediator, IExecutableFilePicker picker)
    {
        _mediator = mediator;
        _picker = picker;
    }

    /// <summary>Path typed in the ffmpeg field. Empty = resolved from the PATH.</summary>
    [ObservableProperty]
    private string _executablePath = string.Empty;

    /// <summary>Path typed in the ffprobe field. Empty = resolved from the PATH.</summary>
    [ObservableProperty]
    private string _ffprobePath = string.Empty;

    /// <summary>
    /// Where the configuration is written, shown in clear so the user can find it, back it up, or
    /// delete it. Read-only: the location belongs to Infrastructure (ADR-0004 storage root).
    /// </summary>
    [ObservableProperty]
    private string _settingsFilePath = string.Empty;

    /// <summary>
    /// False means the paths on screen are the DEFAULTS shipped with the application, not a choice
    /// anyone made. Same two paths in both cases — but "this is what you configured" and "this is
    /// what came in the box" are not the same message.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OriginLabel))]
    private bool _isUserConfigured;

    /// <summary>French rendering of <see cref="IsUserConfigured"/>, shown under the file path.</summary>
    public string OriginLabel => IsUserConfigured
        ? "Ces chemins sont ceux que vous avez enregistrés : ils priment sur les valeurs livrées."
        : "Rien n'a encore été enregistré : les chemins ci-dessus sont les valeurs livrées par défaut.";

    // ------------------------------------------------------------------------ verdict
    //
    // What the last detection, test or save answered. Null message = nothing to say yet, and the
    // InfoBar stays shut.

    [ObservableProperty]
    private string? _verdictTitle;

    [ObservableProperty]
    private string? _verdictMessage;

    [ObservableProperty]
    private StatusSeverity _verdictSeverity = StatusSeverity.Neutral;

    /// <summary>Version line ffmpeg answered with, or null when it did not answer at all.</summary>
    [ObservableProperty]
    private string? _ffmpegVersion;

    /// <summary>Whether the ffmpeg that answered can encode with NVENC — what an NVENC profile needs.</summary>
    [ObservableProperty]
    private bool _isNvencAvailable;

    // ----------------------------------------------------------------------- commands

    /// <summary>Page load: the paths in effect, where they are stored, and whose choice they are.</summary>
    [RelayCommand]
    private Task LoadAsync() => RunGuardedAsync(async () =>
    {
        var settings = await _mediator.SendAsync<GetFfmpegSettingsQuery, FfmpegSettingsView>(
            new GetFfmpegSettingsQuery());

        ExecutablePath = settings.ExecutablePath;
        FfprobePath = settings.FfprobePath;
        SettingsFilePath = settings.SettingsFilePath;

        // After the paths: assigning them clears the verdict, and IsUserConfigured is not a verdict.
        IsUserConfigured = settings.IsUserConfigured;
    });

    /// <summary>
    /// Looks the binaries up where the usual installers put them, then TESTS what it found.
    ///
    /// <para>The two steps belong together. The locator only checks that a file exists — it does not
    /// run it — so a detection alone can hand back a path that turns out not to be an ffmpeg at all.
    /// Chaining the probe is what turns "here are two paths" into a verdict the user can read.</para>
    ///
    /// <para>Detecting CONFIGURES NOTHING: the paths land in the fields and wait for a save.</para>
    /// </summary>
    [RelayCommand]
    private Task DetectAsync() => RunGuardedAsync(async () =>
    {
        var found = await _mediator.SendAsync<DetectFfmpegQuery, FfmpegPathSettings>(new DetectFfmpegQuery());

        // Each field is filled only if something was found FOR IT: the search can come back
        // half-empty (one binary installed by a package manager, the other unzipped by hand), and
        // wiping a path the user had typed correctly is not what "détecter" promises.
        var foundFfmpeg = !string.IsNullOrWhiteSpace(found.ExecutablePath);
        var foundFfprobe = !string.IsNullOrWhiteSpace(found.FfprobePath);

        if (!foundFfmpeg && !foundFfprobe)
        {
            Announce(
                StatusSeverity.Caution,
                "Aucun binaire trouvé",
                "ffmpeg n'a été trouvé ni dans le PATH ni aux emplacements d'installation usuels. "
                + "Indiquez son chemin avec « Parcourir… ».");
            return;
        }

        if (foundFfmpeg)
            ExecutablePath = found.ExecutablePath;

        if (foundFfprobe)
            FfprobePath = found.FfprobePath;

        await ProbeAsync();
    });

    /// <summary>
    /// Runs the environment check on what is IN THE FIELDS, not on what is in effect: the button
    /// must answer for the configuration about to be saved.
    /// </summary>
    [RelayCommand]
    private Task TestAsync() => RunGuardedAsync(ProbeAsync);

    /// <summary>
    /// Saves. The command probes first and writes only if both binaries answer, so a refusal means
    /// NOTHING WAS WRITTEN — and the screen says exactly that instead of a confirmation. Announcing
    /// a save that did not happen is the one lie this screen must never tell: the user would leave
    /// believing the application is configured.
    /// </summary>
    [RelayCommand]
    private Task SaveAsync() => RunGuardedAsync(async () =>
    {
        var result = await _mediator.DispatchAsync<SaveFfmpegSettingsCommand, SaveFfmpegSettingsResult>(
            new SaveFfmpegSettingsCommand(ExecutablePath, FfprobePath));

        var (severity, message) = Interpret(result.Report);

        if (!result.Saved)
        {
            Announce(severity, "Enregistrement refusé", $"{message} Rien n'a été enregistré.");
            return;
        }

        // Written, and applied synchronously by the handler: every consumer — probe, preflight,
        // ffmpeg launch — already reads these paths (ADR-0010).
        IsUserConfigured = true;

        Announce(severity, "Configuration enregistrée", $"Les nouveaux chemins sont actifs. {message}");
    });

    [RelayCommand]
    private Task BrowseFfmpegAsync() => RunGuardedAsync(async () =>
    {
        if (await _picker.PickAsync() is { } path)
            ExecutablePath = path;
    });

    [RelayCommand]
    private Task BrowseFfprobeAsync() => RunGuardedAsync(async () =>
    {
        if (await _picker.PickAsync() is { } path)
            FfprobePath = path;
    });

    // ------------------------------------------------------------------------ internals

    private async Task ProbeAsync()
    {
        var report = await _mediator.SendAsync<ValidateFfmpegPathsQuery, FfmpegEnvironmentReport>(
            new ValidateFfmpegPathsQuery(ExecutablePath, FfprobePath));

        var (severity, message) = Interpret(report);
        Announce(severity, "Environnement ffmpeg", message);
    }

    /// <summary>
    /// Keeps what the probe found, and hands back how it should read on screen. The one place a
    /// report enters this ViewModel.
    /// </summary>
    private (StatusSeverity Severity, string Message) Interpret(FfmpegEnvironmentReport report)
    {
        FfmpegVersion = report.FfmpegVersion;
        IsNvencAvailable = report.NvencAvailable;

        return Verdict(report);
    }

    /// <summary>
    /// Report -> what is shown. The two booleans are the facts; the sentence is their translation.
    ///
    /// <para><see cref="FfmpegEnvironmentReport.Error"/> is deliberately NOT displayed: it is
    /// Infrastructure's own technical English, and the user of this screen reads French. Deriving
    /// the sentence from the structured facts instead is the same discipline the dashboard applies
    /// to <c>StartBlockReason</c> — and it survives a rewording of the probe.</para>
    /// </summary>
    private static (StatusSeverity Severity, string Message) Verdict(FfmpegEnvironmentReport report)
        => (report.FfmpegAvailable, report.FfprobeAvailable) switch
        {
            (true, true) => (StatusSeverity.Success, Describe(report)),

            (false, true) => (StatusSeverity.Critical,
                "ffmpeg est introuvable ou ne répond pas. Vérifiez son chemin, ou laissez le champ "
                + "vide si ffmpeg est dans le PATH."),

            (true, false) => (StatusSeverity.Critical,
                "ffprobe est introuvable ou ne répond pas. Vérifiez son chemin, ou laissez le champ "
                + "vide si ffprobe est dans le PATH."),

            _ => (StatusSeverity.Critical,
                "Ni ffmpeg ni ffprobe ne répondent. Vérifiez leurs chemins, ou laissez les champs "
                + "vides s'ils sont dans le PATH.")
        };

    /// <summary>
    /// What a working toolchain has to say for itself: which ffmpeg answered, and whether it can
    /// encode with NVENC — the one capability a profile can require and not get.
    /// </summary>
    private static string Describe(FfmpegEnvironmentReport report)
        => $"ffmpeg et ffprobe répondent. {report.FfmpegVersion ?? "Version inconnue"} · "
            + $"NVENC {(report.NvencAvailable ? "disponible" : "indisponible")}.";

    private void Announce(StatusSeverity severity, string title, string message)
    {
        VerdictSeverity = severity;
        VerdictTitle = title;
        VerdictMessage = message;
    }

    // A verdict belongs to the pair of paths it was produced for. Leaving "ffmpeg répond" on screen
    // while the user edits the path below it would state something that has not been checked.
    partial void OnExecutablePathChanged(string value) => ClearVerdict();

    partial void OnFfprobePathChanged(string value) => ClearVerdict();

    private void ClearVerdict()
    {
        VerdictSeverity = StatusSeverity.Neutral;
        VerdictTitle = null;
        VerdictMessage = null;
        FfmpegVersion = null;
        IsNvencAvailable = false;
    }
}
