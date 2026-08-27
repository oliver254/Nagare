using System.Diagnostics;
using Nagare.Application.Abstractions;

namespace Nagare.Infrastructure.Ffmpeg;

/// <summary>
/// Startup environment check (ARCHITECTURE.md §4.2). Verifies ffmpeg/ffprobe presence and
/// NVENC availability via `ffmpeg -encoders`. Gracefully reports absence (binaries are not
/// on the dev machine — addendum SPEC).
///
/// <para>Two entry points: the paths in effect, and an arbitrary pair. The second is what the
/// settings screen tests before saving (ADR-0010) — and the first is written in terms of it, so
/// the two verdicts can never be produced by different code.</para>
/// </summary>
public sealed class FfmpegEnvironmentProbe : IFfmpegEnvironmentProbe
{
    /// <summary>
    /// Time granted to ONE binary launch before it is declared unusable.
    ///
    /// <para>Ten seconds is not a performance budget: ffmpeg answers <c>-version</c> in
    /// milliseconds and <c>-encoders</c> in a fraction of a second — a first launch from a cold
    /// disk included, which is the slowest legitimate case there is. Anything past that bound is
    /// not a slow ffmpeg, it is not an ffmpeg at all: the file picker of the settings screen only
    /// filters on ".exe" (ADR-0010), so a GUI application designated by mistake starts, opens a
    /// window and never gives the hand back. Exceeding the delay therefore means exactly what a
    /// missing binary means — unavailable — and the process is killed rather than left behind.</para>
    ///
    /// <para>PER LAUNCH: a full check runs up to three of them (ffmpeg, ffprobe, then the encoder
    /// list), so a check made entirely of hanging binaries answers in three times this. Bounding
    /// the check as a whole would be a coarser tool for no gain — each launch already tells the
    /// truth about one binary, and the second is worth starting even when the first gave up.</para>
    /// </summary>
    public static readonly TimeSpan DefaultLaunchTimeout = TimeSpan.FromSeconds(10);

    private readonly IFfmpegPaths _paths;
    private readonly TimeSpan _launchTimeout;

    /// <summary>Production constructor: each launch gets <see cref="DefaultLaunchTimeout"/>.</summary>
    public FfmpegEnvironmentProbe(IFfmpegPaths paths) : this(paths, DefaultLaunchTimeout)
    {
    }

    /// <summary>
    /// Testing seam: the deadline is given, so the giving-up path can be asserted in milliseconds
    /// instead of making the suite wait ten seconds for it.
    /// </summary>
    public FfmpegEnvironmentProbe(IFfmpegPaths paths, TimeSpan launchTimeout)
    {
        ArgumentNullException.ThrowIfNull(paths);

        _paths = paths;
        _launchTimeout = launchTimeout;
    }

    // Read at CALL time, never cached in a field: this is a singleton, and the user can change
    // the paths while it lives (ADR-0010).
    public Task<FfmpegEnvironmentReport> CheckAsync(CancellationToken ct) => CheckAsync(_paths.Current, ct);

    public async Task<FfmpegEnvironmentReport> CheckAsync(FfmpegPathSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            var version = await TryRunAsync(settings.ResolvedFfmpeg, ["-version"], ct);
            var ffmpegAvailable = version is not null;
            var ffprobeAvailable = await TryRunAsync(settings.ResolvedFfprobe, ["-version"], ct) is not null;

            var nvenc = false;
            if (ffmpegAvailable)
            {
                var encoders = await TryRunAsync(settings.ResolvedFfmpeg, ["-hide_banner", "-encoders"], ct);
                nvenc = encoders?.Contains("h264_nvenc", StringComparison.OrdinalIgnoreCase) == true;
            }

            var versionLine = version?.Split('\n', 2)[0].Trim();

            return new FfmpegEnvironmentReport(
                ffmpegAvailable,
                ffprobeAvailable,
                versionLine,
                nvenc,
                DescribeMissing(ffmpegAvailable, ffprobeAvailable));
        }
        catch (Exception ex)
        {
            return new FfmpegEnvironmentReport(false, false, null, false, ex.Message);
        }
    }

    /// <summary>
    /// ffprobe gets its own sentence. The save refuses on either binary, so a report that only
    /// ever named ffmpeg would leave a refusal without a reason.
    /// </summary>
    private static string? DescribeMissing(bool ffmpegAvailable, bool ffprobeAvailable)
        => (ffmpegAvailable, ffprobeAvailable) switch
        {
            (true, true) => null,
            (false, true) => "ffmpeg not found (configured path or PATH).",
            (true, false) => "ffprobe not found (configured path or PATH).",
            _ => "ffmpeg and ffprobe not found (configured path or PATH)."
        };

    private async Task<string?> TryRunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        // One deadline per launch, linked to the caller's token so whichever fires first ends the
        // wait. The caller's token is CancellationToken.None in practice — neither the settings
        // screen nor the query handler has one to give — which is precisely why the probe cannot
        // rely on it to ever come back.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(_launchTimeout);

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            process = Process.Start(startInfo);
            if (process is null)
                return null;

            var output = await process.StandardOutput.ReadToEndAsync(deadline.Token);
            await process.WaitForExitAsync(deadline.Token);
            return output;
        }
        catch
        {
            // Missing binary, unlaunchable file, or a process that outlived its deadline: the three
            // say the same thing to the caller — this is not a usable ffmpeg.
            KillIfStillRunning(process);
            return null;
        }
        finally
        {
            process?.Dispose();
        }
    }

    /// <summary>
    /// Kills what did not come back on its own, WITH ITS CHILDREN — the discipline
    /// <c>FfmpegProcessRunner.StopAsync</c> already applies to a stubborn ffmpeg. No grace period is
    /// offered here: the process was given the whole deadline, it broadcasts nothing and has nothing
    /// to flush. Abandoning the wait while leaving it alive would be a worse defect than the freeze
    /// this guards against — the screen would answer, and the stray window would stay on the desktop.
    /// </summary>
    private static void KillIfStillRunning(Process? process)
    {
        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Already gone, or out of reach. Nothing more can be done from here, and the verdict
            // handed to the user does not change: unavailable.
        }
    }
}
