using Nagare.Application.Abstractions;

namespace Nagare.Infrastructure.Ffmpeg;

/// <summary>
/// Finds ffmpeg/ffprobe where the usual installers put them (ADR-0010). Scans a candidate
/// directory list in order and keeps the FIRST hit for each binary, independently: winget may
/// have shimmed one while the other sits in a hand-unzipped folder.
///
/// <para>Existence only — the locator proposes, the probe decides. Running each candidate to see
/// whether it answers would turn a detection into a series of process launches, and the user is
/// going to press "Test" on the result anyway.</para>
/// </summary>
public sealed class FfmpegLocator : IFfmpegLocator
{
    // ".exe" first, then the bare name: on Windows only the former exists, and the bare name
    // keeps the locator meaningful for a test that writes plain files.
    private static readonly string[] FfmpegFileNames = ["ffmpeg.exe", "ffmpeg"];
    private static readonly string[] FfprobeFileNames = ["ffprobe.exe", "ffprobe"];

    private readonly IReadOnlyList<string> _candidateDirectories;

    /// <summary>Production constructor: the candidate directories are read from the environment.</summary>
    public FfmpegLocator() : this(DefaultCandidateDirectories())
    {
    }

    /// <summary>
    /// Testing seam: the directories are given, so the search order can be asserted without
    /// depending on what happens to be installed on the machine running the tests.
    /// </summary>
    public FfmpegLocator(IReadOnlyList<string> candidateDirectories)
    {
        ArgumentNullException.ThrowIfNull(candidateDirectories);
        _candidateDirectories = candidateDirectories;
    }

    public Task<FfmpegPathSettings> LocateAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var ffmpeg = FindFirst(FfmpegFileNames, ct);
        var ffprobe = FindFirst(FfprobeFileNames, ct);

        return Task.FromResult(new FfmpegPathSettings(ffmpeg ?? string.Empty, ffprobe ?? string.Empty));
    }

    /// <summary>
    /// PATH first — an installation the shell already resolves is the one the user expects — then
    /// the package managers, then the two common manual install folders.
    /// </summary>
    private static IReadOnlyList<string> DefaultCandidateDirectories()
    {
        var directories = new List<string>();
        directories.AddRange(PathDirectories());

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        AddIfRooted(directories, localAppData, "Microsoft", "WinGet", "Links");
        AddIfRooted(directories, programData, "chocolatey", "bin");
        AddIfRooted(directories, programFiles, "ffmpeg", "bin");
        AddIfRooted(directories, localAppData, "Programs", "ffmpeg", "bin");
        directories.Add(@"C:\ffmpeg\bin");

        return directories;
    }

    private static IEnumerable<string> PathDirectories()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return [];

        return path
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            // The PATH is user-editable and routinely holds quoted or malformed entries; one of
            // them must not take the whole detection down.
            .Where(entry => entry.IndexOfAny(Path.GetInvalidPathChars()) < 0);
    }

    private static void AddIfRooted(List<string> directories, string root, params string[] segments)
    {
        // GetFolderPath returns "" for a folder the platform does not know: skip rather than
        // build a relative path that would be resolved against the current directory.
        if (string.IsNullOrEmpty(root))
            return;

        directories.Add(Path.Combine([root, .. segments]));
    }

    private string? FindFirst(IReadOnlyList<string> fileNames, CancellationToken ct)
    {
        foreach (var directory in _candidateDirectories)
        {
            ct.ThrowIfCancellationRequested();

            foreach (var fileName in fileNames)
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(directory, fileName);
                }
                catch (ArgumentException)
                {
                    break;   // Unusable directory entry: move on to the next one.
                }

                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }
}
