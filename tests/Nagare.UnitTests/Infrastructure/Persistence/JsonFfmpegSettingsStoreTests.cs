using Microsoft.Extensions.Options;
using Nagare.Application.Abstractions;
using Nagare.Infrastructure.Persistence;
using Nagare.UnitTests.Fakes;

namespace Nagare.UnitTests.Infrastructure.Persistence;

/// <summary>
/// The settings file, exercised against a REAL temporary directory. Mocking the file system here
/// would test the mock: what is at stake — an absent file, an empty one, a truncated one, the
/// File.Replace branch that only fires on the second write — is file-system behaviour.
/// </summary>
public sealed class JsonFfmpegSettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "nagare-tests", Guid.NewGuid().ToString("N"));

    private readonly CapturingLogger<JsonFfmpegSettingsStore> _logger = new();

    // ------------------------------------------------------------------- round trip

    [Fact]
    public async Task What_is_saved_is_what_comes_back()
    {
        var store = CreateStore();
        var settings = new FfmpegPathSettings(@"C:\tools\ffmpeg\bin\ffmpeg.exe", @"C:\tools\ffmpeg\bin\ffprobe.exe");

        await store.SaveAsync(settings, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(settings, loaded);
    }

    /// <summary>
    /// An empty field is a CHOICE — "take it from the PATH" — not an absence. Coming back as
    /// "ffmpeg" would silently rewrite what the user asked for.
    /// </summary>
    [Fact]
    public async Task An_empty_path_survives_the_round_trip_as_empty()
    {
        var store = CreateStore();

        await store.SaveAsync(new FfmpegPathSettings(string.Empty, @"C:\tools\ffprobe.exe"),
            CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(string.Empty, loaded!.ExecutablePath);
        Assert.Equal(@"C:\tools\ffprobe.exe", loaded.FfprobePath);
    }

    /// <summary>The second write takes the File.Replace branch; the first took File.Move.</summary>
    [Fact]
    public async Task A_second_save_replaces_the_first()
    {
        var store = CreateStore();

        await store.SaveAsync(new FfmpegPathSettings(@"C:\one\ffmpeg.exe", @"C:\one\ffprobe.exe"),
            CancellationToken.None);
        await store.SaveAsync(new FfmpegPathSettings(@"C:\two\ffmpeg.exe", @"C:\two\ffprobe.exe"),
            CancellationToken.None);

        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(@"C:\two\ffmpeg.exe", loaded!.ExecutablePath);
        Assert.False(File.Exists(store.SettingsFilePath + ".tmp"));
    }

    [Fact]
    public async Task The_file_lands_where_SettingsFilePath_announces_it()
    {
        var store = CreateStore();

        await store.SaveAsync(FfmpegPathSettings.Empty, CancellationToken.None);

        Assert.Equal(Path.Combine(_root, "settings.json"), store.SettingsFilePath);
        Assert.True(File.Exists(store.SettingsFilePath));
    }

    /// <summary>Saving into a folder that does not exist yet is the FIRST save of a fresh install.</summary>
    [Fact]
    public async Task The_storage_directory_is_created_on_demand()
    {
        var store = CreateStore();
        Assert.False(Directory.Exists(_root));

        await store.SaveAsync(FfmpegPathSettings.Empty, CancellationToken.None);

        Assert.True(File.Exists(store.SettingsFilePath));
    }

    // ------------------------------------------------------------- nothing to load

    [Fact]
    public async Task No_file_reads_as_never_configured()
    {
        var loaded = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task An_empty_file_reads_as_never_configured()
    {
        var store = CreateStore();
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(store.SettingsFilePath, string.Empty, CancellationToken.None);

        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Null(loaded);
    }

    /// <summary>
    /// The case that decides whether the application opens at all. A truncated write, a hand-edit
    /// gone wrong — the answer is "nothing configured", not an exception thrown from a hosted
    /// service, because the screen that repairs the file is inside the application (ADR-0010).
    /// </summary>
    [Theory]
    [InlineData("{ \"ExecutablePath\": ")]
    [InlineData("this is not json at all")]
    [InlineData("[]")]
    public async Task A_malformed_file_reads_as_never_configured_and_is_logged(string content)
    {
        var store = CreateStore();
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(store.SettingsFilePath, content, CancellationToken.None);

        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Null(loaded);
        Assert.Contains(_logger.Messages, m => m.Contains("settings.json", StringComparison.Ordinal));
    }

    /// <summary>A malformed file must not be silently swallowed either: a repair must stay possible.</summary>
    [Fact]
    public async Task A_malformed_file_can_still_be_overwritten_by_a_save()
    {
        var store = CreateStore();
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(store.SettingsFilePath, "{ broken", CancellationToken.None);

        await store.SaveAsync(new FfmpegPathSettings(@"C:\ok\ffmpeg.exe", @"C:\ok\ffprobe.exe"),
            CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(@"C:\ok\ffmpeg.exe", loaded!.ExecutablePath);
    }

    // ------------------------------------------------------------------- fixtures

    private JsonFfmpegSettingsStore CreateStore()
        => new(Options.Create(new NagareStorageOptions { RootDirectory = _root }), _logger);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
