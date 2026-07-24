using Nagare.Application.Abstractions;
using Nagare.Application.Settings;
using Nagare.UnitTests.Fakes;
using Nagare.ViewModels;

namespace Nagare.UnitTests.ViewModels;

/// <summary>
/// The settings screen (ADR-0010). No UI involved: the ViewModel only knows an IMediator and a file
/// picker port.
///
/// <para>The messages are answered by the REAL Application handlers, not by canned results — the
/// same choice DashboardViewModelTests makes for the preflight. What must be proven here is that the
/// screen obeys the rule and translates it; so the thing it talks to has to be the thing that
/// decides. "Saved = false means nothing was written" is worth nothing if the false comes from the
/// test itself.</para>
/// </summary>
public sealed class SettingsViewModelTests
{
    // Fictional paths: a test must not depend on — nor disclose — what is installed anywhere.
    private const string Ffmpeg = @"C:\tools\ffmpeg\bin\ffmpeg.exe";
    private const string Ffprobe = @"C:\tools\ffmpeg\bin\ffprobe.exe";

    // ------------------------------------------------------------------------ load

    [Fact]
    public async Task Load_shows_the_paths_in_effect_and_where_the_choice_is_written()
    {
        var fixture = Create(inEffect: new FfmpegPathSettings(Ffmpeg, Ffprobe));

        await fixture.Vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(Ffmpeg, fixture.Vm.ExecutablePath);
        Assert.Equal(Ffprobe, fixture.Vm.FfprobePath);
        Assert.Equal(fixture.Store.SettingsFilePath, fixture.Vm.SettingsFilePath);
        Assert.Null(fixture.Vm.ErrorMessage);

        // Opening the page checks nothing: a verdict must belong to an action the user took.
        Assert.Null(fixture.Vm.VerdictMessage);
    }

    /// <summary>
    /// Same paths on screen, two different truths: shipped defaults, or the user's own choice. The
    /// screen has to tell them apart — otherwise a blank install looks configured.
    /// </summary>
    [Fact]
    public async Task Load_tells_a_shipped_default_apart_from_a_saved_choice()
    {
        var untouched = Create(inEffect: new FfmpegPathSettings(Ffmpeg, Ffprobe));
        await untouched.Vm.LoadCommand.ExecuteAsync(null);

        Assert.False(untouched.Vm.IsUserConfigured);
        Assert.Contains("par défaut", untouched.Vm.OriginLabel);

        var configured = Create(stored: new FfmpegPathSettings(Ffmpeg, Ffprobe));
        await configured.Vm.LoadCommand.ExecuteAsync(null);

        Assert.True(configured.Vm.IsUserConfigured);
        Assert.DoesNotContain("par défaut", configured.Vm.OriginLabel);
    }

    // --------------------------------------------------------------------- detection

    /// <summary>
    /// Detection fills the fields AND tests them: the locator only checks that a file exists, so
    /// without the chained probe the screen would report a find it never ran.
    /// </summary>
    [Fact]
    public async Task Detect_fills_the_fields_and_tests_what_it_found()
    {
        var fixture = Create(
            runnable: [Ffmpeg, Ffprobe],
            detected: new FfmpegPathSettings(Ffmpeg, Ffprobe));

        await fixture.Vm.LoadCommand.ExecuteAsync(null);
        await fixture.Vm.DetectCommand.ExecuteAsync(null);

        Assert.Equal(Ffmpeg, fixture.Vm.ExecutablePath);
        Assert.Equal(Ffprobe, fixture.Vm.FfprobePath);

        Assert.Equal(StatusSeverity.Success, fixture.Vm.VerdictSeverity);
        Assert.Single(fixture.Mediator.Sent.OfType<ValidateFfmpegPathsQuery>());

        // Detecting CONFIGURES NOTHING: the proposal waits for a save.
        Assert.Equal(0, fixture.Store.SaveCallCount);
        Assert.Equal(0, fixture.Paths.ApplyCallCount);
        Assert.False(fixture.Vm.IsUserConfigured);
    }

    [Fact]
    public async Task Detect_that_finds_nothing_says_so_and_leaves_the_fields_alone()
    {
        var fixture = Create(
            inEffect: new FfmpegPathSettings(Ffmpeg, Ffprobe),
            detected: FfmpegPathSettings.Empty);

        await fixture.Vm.LoadCommand.ExecuteAsync(null);
        await fixture.Vm.DetectCommand.ExecuteAsync(null);

        Assert.Equal(Ffmpeg, fixture.Vm.ExecutablePath);
        Assert.Equal(Ffprobe, fixture.Vm.FfprobePath);

        Assert.Equal(StatusSeverity.Caution, fixture.Vm.VerdictSeverity);
        Assert.NotNull(fixture.Vm.VerdictMessage);
        Assert.Contains("Parcourir", fixture.Vm.VerdictMessage);   // it names the way out

        // Nothing was found, so there is nothing to run: no probe, no false verdict.
        Assert.Empty(fixture.Mediator.Sent.OfType<ValidateFfmpegPathsQuery>());
    }

    /// <summary>
    /// The search can come back half-empty — one binary installed by a package manager, the other
    /// unzipped by hand. Overwriting the half the user had already got right is not what "détecter"
    /// promises.
    /// </summary>
    [Fact]
    public async Task Detect_never_wipes_a_path_it_could_not_improve()
    {
        var fixture = Create(
            runnable: [Ffmpeg, Ffprobe],
            inEffect: new FfmpegPathSettings(@"C:\old\ffmpeg.exe", Ffprobe),
            detected: new FfmpegPathSettings(Ffmpeg, string.Empty));

        await fixture.Vm.LoadCommand.ExecuteAsync(null);
        await fixture.Vm.DetectCommand.ExecuteAsync(null);

        Assert.Equal(Ffmpeg, fixture.Vm.ExecutablePath);    // replaced by the find
        Assert.Equal(Ffprobe, fixture.Vm.FfprobePath);      // kept: nothing better was found
    }

    // -------------------------------------------------------------------------- test

    [Fact]
    public async Task A_successful_test_reports_the_version_and_NVENC()
    {
        var fixture = Create(
            runnable: [Ffmpeg, Ffprobe],
            inEffect: new FfmpegPathSettings(Ffmpeg, Ffprobe));

        await fixture.Vm.LoadCommand.ExecuteAsync(null);
        await fixture.Vm.TestCommand.ExecuteAsync(null);

        Assert.Equal(StatusSeverity.Success, fixture.Vm.VerdictSeverity);
        Assert.True(fixture.Vm.IsNvencAvailable);
        Assert.NotNull(fixture.Vm.FfmpegVersion);

        Assert.NotNull(fixture.Vm.VerdictMessage);
        Assert.Contains(fixture.Vm.FfmpegVersion, fixture.Vm.VerdictMessage);
        Assert.Contains("NVENC disponible", fixture.Vm.VerdictMessage);

        // Testing writes nothing either.
        Assert.Equal(0, fixture.Store.SaveCallCount);
    }

    /// <summary>
    /// The probe answers in technical English ("ffmpeg not found (configured path or PATH)."). The
    /// user of this screen reads French, and the sentence is derived from the structured facts —
    /// never from that string.
    /// </summary>
    [Fact]
    public async Task A_failed_test_names_the_binary_at_fault_in_French()
    {
        var fixture = Create(
            runnable: [Ffprobe],                              // ffmpeg does not answer
            inEffect: new FfmpegPathSettings(Ffmpeg, Ffprobe));

        await fixture.Vm.LoadCommand.ExecuteAsync(null);
        await fixture.Vm.TestCommand.ExecuteAsync(null);

        Assert.Equal(StatusSeverity.Critical, fixture.Vm.VerdictSeverity);
        Assert.NotNull(fixture.Vm.VerdictMessage);
        Assert.Contains("ffmpeg", fixture.Vm.VerdictMessage);
        Assert.Contains("introuvable", fixture.Vm.VerdictMessage);
        Assert.DoesNotContain("not found", fixture.Vm.VerdictMessage);

        Assert.Null(fixture.Vm.FfmpegVersion);
        Assert.False(fixture.Vm.IsNvencAvailable);
    }

    /// <summary>A verdict belongs to the pair it was produced for, and to no other.</summary>
    [Fact]
    public async Task Editing_a_path_drops_the_verdict_that_was_not_about_it()
    {
        var fixture = Create(
            runnable: [Ffmpeg, Ffprobe],
            inEffect: new FfmpegPathSettings(Ffmpeg, Ffprobe));

        await fixture.Vm.LoadCommand.ExecuteAsync(null);
        await fixture.Vm.TestCommand.ExecuteAsync(null);

        Assert.NotNull(fixture.Vm.VerdictMessage);

        fixture.Vm.ExecutablePath = @"C:\elsewhere\ffmpeg.exe";

        Assert.Null(fixture.Vm.VerdictMessage);
        Assert.Equal(StatusSeverity.Neutral, fixture.Vm.VerdictSeverity);
        Assert.Null(fixture.Vm.FfmpegVersion);
        Assert.False(fixture.Vm.IsNvencAvailable);
    }

    // -------------------------------------------------------------------------- save

    /// <summary>
    /// The one lie this screen must never tell. A refused save wrote NOTHING, and a confirmation
    /// would send the user away believing the application is configured.
    /// </summary>
    [Fact]
    public async Task A_refused_save_writes_nothing_and_shows_no_confirmation()
    {
        var fixture = Create(runnable: [Ffprobe]);   // ffmpeg does not answer

        await fixture.Vm.LoadCommand.ExecuteAsync(null);
        fixture.Vm.ExecutablePath = Ffmpeg;
        fixture.Vm.FfprobePath = Ffprobe;

        await fixture.Vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, fixture.Store.SaveCallCount);
        Assert.Equal(0, fixture.Paths.ApplyCallCount);
        Assert.False(fixture.Vm.IsUserConfigured);

        Assert.Equal(StatusSeverity.Critical, fixture.Vm.VerdictSeverity);
        Assert.Equal("Enregistrement refusé", fixture.Vm.VerdictTitle);
        Assert.NotNull(fixture.Vm.VerdictMessage);
        Assert.Contains("Rien n'a été enregistré", fixture.Vm.VerdictMessage);
    }

    [Fact]
    public async Task A_successful_save_confirms_and_the_paths_take_effect()
    {
        var fixture = Create(runnable: [Ffmpeg, Ffprobe]);

        await fixture.Vm.LoadCommand.ExecuteAsync(null);
        fixture.Vm.ExecutablePath = Ffmpeg;
        fixture.Vm.FfprobePath = Ffprobe;

        await fixture.Vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.Store.SaveCallCount);
        Assert.Equal(new FfmpegPathSettings(Ffmpeg, Ffprobe), fixture.Store.Stored);
        Assert.Equal(new FfmpegPathSettings(Ffmpeg, Ffprobe), fixture.Paths.Current);

        Assert.Equal(StatusSeverity.Success, fixture.Vm.VerdictSeverity);
        Assert.Equal("Configuration enregistrée", fixture.Vm.VerdictTitle);
        Assert.True(fixture.Vm.IsUserConfigured);
        Assert.Contains("enregistrés", fixture.Vm.OriginLabel);
        Assert.Null(fixture.Vm.ErrorMessage);
    }

    /// <summary>
    /// An empty field is a legitimate value — "resolve it from the PATH" — and nothing in the UI may
    /// refuse it. Whether it works is the probe's answer, not a rule restated here.
    /// </summary>
    [Fact]
    public async Task Empty_fields_are_accepted_and_mean_the_PATH()
    {
        // The bare names stand for "the PATH resolves them", see FakeFfmpegEnvironmentProbe.
        var fixture = Create(runnable: ["ffmpeg", "ffprobe"]);

        await fixture.Vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, fixture.Vm.ExecutablePath);
        Assert.Equal(string.Empty, fixture.Vm.FfprobePath);

        await fixture.Vm.TestCommand.ExecuteAsync(null);
        Assert.Equal(StatusSeverity.Success, fixture.Vm.VerdictSeverity);

        await fixture.Vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.Store.SaveCallCount);
        Assert.Equal(FfmpegPathSettings.Empty, fixture.Store.Stored);
        Assert.Null(fixture.Vm.ErrorMessage);
    }

    // ------------------------------------------------------------------------ browse

    [Fact]
    public async Task Browsing_fills_the_field_it_was_started_from()
    {
        var forFfmpeg = Create(picked: Ffmpeg);
        await forFfmpeg.Vm.BrowseFfmpegCommand.ExecuteAsync(null);

        Assert.Equal(Ffmpeg, forFfmpeg.Vm.ExecutablePath);
        Assert.Equal(string.Empty, forFfmpeg.Vm.FfprobePath);

        var forFfprobe = Create(picked: Ffprobe);
        await forFfprobe.Vm.BrowseFfprobeCommand.ExecuteAsync(null);

        Assert.Equal(Ffprobe, forFfprobe.Vm.FfprobePath);
        Assert.Equal(string.Empty, forFfprobe.Vm.ExecutablePath);
    }

    [Fact]
    public async Task A_cancelled_browse_leaves_the_field_alone()
    {
        var fixture = Create(picked: null, inEffect: new FfmpegPathSettings(Ffmpeg, Ffprobe));

        await fixture.Vm.LoadCommand.ExecuteAsync(null);
        await fixture.Vm.BrowseFfmpegCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.Picker.PickCallCount);
        Assert.Equal(Ffmpeg, fixture.Vm.ExecutablePath);
    }

    // ---------------------------------------------------------------------- fixtures

    private sealed record Fixture(
        SettingsViewModel Vm,
        FakeMediator Mediator,
        FakeFfmpegSettingsStore Store,
        FakeFfmpegPaths Paths,
        FakeExecutableFilePicker Picker);

    /// <param name="runnable">Binaries that answer. A bare "ffmpeg"/"ffprobe" stands for "the PATH
    /// resolves it", which is how an empty field is exercised without depending on the machine.</param>
    /// <param name="detected">What the locator proposes. Empty = it found nothing.</param>
    /// <param name="stored">What is in settings.json. Null = the user never configured anything.</param>
    /// <param name="inEffect">The paths currently in force; defaults to <paramref name="stored"/>.</param>
    /// <param name="picked">What the file dialog returns. Null = the user cancelled.</param>
    private static Fixture Create(
        string[]? runnable = null,
        FfmpegPathSettings? detected = null,
        FfmpegPathSettings? stored = null,
        FfmpegPathSettings? inEffect = null,
        string? picked = null)
    {
        var probe = new FakeFfmpegEnvironmentProbe(runnable ?? []);
        var store = new FakeFfmpegSettingsStore { Stored = stored };
        var paths = new FakeFfmpegPaths(inEffect ?? stored);
        var picker = new FakeExecutableFilePicker(picked);

        var read = new GetFfmpegSettingsHandler(paths, store);
        var detect = new DetectFfmpegHandler(new FakeFfmpegLocator(detected));
        var validate = new ValidateFfmpegPathsHandler(probe);
        var save = new SaveFfmpegSettingsHandler(probe, store, paths);

        var mediator = new FakeMediator()
            .Answer<GetFfmpegSettingsQuery>(query => read.Handle(query).GetAwaiter().GetResult())
            .Answer<DetectFfmpegQuery>(query => detect.Handle(query).GetAwaiter().GetResult())
            .Answer<ValidateFfmpegPathsQuery>(query => validate.Handle(query).GetAwaiter().GetResult())
            .Answer<SaveFfmpegSettingsCommand>(command => save.Handle(command).GetAwaiter().GetResult());

        return new Fixture(new SettingsViewModel(mediator, picker), mediator, store, paths, picker);
    }
}
