using Nagare.Application.Abstractions;
using Nagare.Infrastructure.Ffmpeg;

namespace Nagare.UnitTests.Infrastructure.Ffmpeg;

/// <summary>
/// Detection, run over directories this test creates. The production list starts with the PATH of
/// the machine, so asserting anything about it directly would make the suite depend on whether
/// the agent running it happens to have ffmpeg installed — the injected list is what makes the
/// search order an assertable fact.
/// </summary>
public sealed class FfmpegLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "nagare-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Nothing_installed_yields_nothing()
    {
        var empty = CreateDirectory("empty");

        var found = await new FfmpegLocator([empty]).LocateAsync(CancellationToken.None);

        Assert.Equal(string.Empty, found.ExecutablePath);
        Assert.Equal(string.Empty, found.FfprobePath);
    }

    [Fact]
    public async Task A_directory_that_does_not_exist_is_not_a_failure()
    {
        var found = await new FfmpegLocator([Path.Combine(_root, "nowhere")]).LocateAsync(CancellationToken.None);

        Assert.Equal(FfmpegPathSettings.Empty, found);
    }

    [Fact]
    public async Task Both_binaries_in_one_directory_are_found()
    {
        var bin = CreateDirectory("bin");
        var ffmpeg = CreateBinary(bin, "ffmpeg.exe");
        var ffprobe = CreateBinary(bin, "ffprobe.exe");

        var found = await new FfmpegLocator([bin]).LocateAsync(CancellationToken.None);

        Assert.Equal(ffmpeg, found.ExecutablePath);
        Assert.Equal(ffprobe, found.FfprobePath);
    }

    /// <summary>
    /// The half-found case, and the reason the two binaries are searched independently: a winget
    /// shim for one and a hand-unzipped folder for the other is a real installation.
    /// </summary>
    [Fact]
    public async Task ffmpeg_alone_is_reported_alone()
    {
        var bin = CreateDirectory("bin");
        var ffmpeg = CreateBinary(bin, "ffmpeg.exe");

        var found = await new FfmpegLocator([bin]).LocateAsync(CancellationToken.None);

        Assert.Equal(ffmpeg, found.ExecutablePath);
        Assert.Equal(string.Empty, found.FfprobePath);
    }

    [Fact]
    public async Task Each_binary_is_taken_from_the_first_directory_that_has_it()
    {
        var first = CreateDirectory("first");
        var second = CreateDirectory("second");
        var ffprobe = CreateBinary(first, "ffprobe.exe");
        CreateBinary(second, "ffprobe.exe");
        var ffmpeg = CreateBinary(second, "ffmpeg.exe");

        var found = await new FfmpegLocator([first, second]).LocateAsync(CancellationToken.None);

        Assert.Equal(ffprobe, found.FfprobePath);   // first wins
        Assert.Equal(ffmpeg, found.ExecutablePath); // only the second had it
    }

    /// <summary>
    /// The default list is built from the environment — a PATH that may hold quoted or malformed
    /// entries, special folders the platform may not know. It must produce an answer, whatever is
    /// installed on the machine running this.
    /// </summary>
    [Fact]
    public async Task The_environment_backed_locator_answers_without_throwing()
    {
        var found = await new FfmpegLocator().LocateAsync(CancellationToken.None);

        Assert.NotNull(found);
    }

    // ------------------------------------------------------------------- fixtures

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateBinary(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, "not a real binary");
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
