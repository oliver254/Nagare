using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nagare.Application.Abstractions;

namespace Nagare.Infrastructure.Ffmpeg;

/// <summary>
/// Wraps a single ffmpeg <see cref="Process"/> (ARCHITECTURE.md §6.2). Reads stderr/stdout
/// line by line; every line is scrubbed (§6.3) before being emitted. Clean stop: 'q' on
/// stdin, wait gracePeriod, otherwise Kill(entireProcessTree: true).
/// </summary>
public sealed class FfmpegProcessRunner(
    IOptions<FfmpegOptions> options,
    ILogger<FfmpegProcessRunner> logger) : IFfmpegProcessRunner
{
    private readonly FfmpegOptions _options = options.Value;

    private Process? _process;
    private StreamKeyScrubber? _scrubber;

    public bool IsRunning => _process is { HasExited: false };

    public event Action<string>? OutputLineReceived;
    public event Action<FfmpegStats>? StatsReceived;
    public event Action<int>? Exited;

    public Task StartAsync(FfmpegCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (_process is not null)
            throw new InvalidOperationException("This runner already started a process. Use a fresh runner per launch.");

        _scrubber = new StreamKeyScrubber(command.Secrets);

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ResolvedFfmpeg,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in command.Arguments)
            startInfo.ArgumentList.Add(argument);

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.ErrorDataReceived += (_, e) => OnLine(e.Data);
        process.OutputDataReceived += (_, e) => OnLine(e.Data);
        process.Exited += (_, _) => OnExited();

        _process = process;

        ct.ThrowIfCancellationRequested();
        process.Start();
        process.BeginErrorReadLine();   // ffmpeg writes progress on stderr
        process.BeginOutputReadLine();

        return Task.CompletedTask;
    }

    public async Task StopAsync(TimeSpan gracePeriod, CancellationToken ct)
    {
        var process = _process;
        if (process is null || process.HasExited)
            return;

        try
        {
            // Ask ffmpeg to quit gracefully.
            await process.StandardInput.WriteLineAsync("q");
            await process.StandardInput.FlushAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not write 'q' to ffmpeg stdin; will kill instead.");
        }

        try
        {
            using var graceCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            graceCts.CancelAfter(gracePeriod);
            await process.WaitForExitAsync(graceCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to kill the ffmpeg process tree.");
                }
            }
        }
    }

    private void OnLine(string? line)
    {
        if (line is null)
            return;

        var scrubbed = _scrubber?.Scrub(line) ?? line;
        OutputLineReceived?.Invoke(scrubbed);

        // Parse from the scrubbed line: stats fields never contain the key.
        var stats = FfmpegStatsParser.TryParse(scrubbed);
        if (stats is not null)
            StatsReceived?.Invoke(stats);
    }

    private void OnExited()
    {
        var code = _process?.ExitCode ?? -1;
        Exited?.Invoke(code);
    }

    public async ValueTask DisposeAsync()
    {
        var process = _process;
        if (process is null)
            return;

        _process = null;

        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error while killing ffmpeg on dispose.");
        }

        process.Dispose();
        await ValueTask.CompletedTask;
    }
}

/// <summary>Creates a fresh <see cref="FfmpegProcessRunner"/> per launch (see coordinator).</summary>
public sealed class FfmpegProcessRunnerFactory(
    IOptions<FfmpegOptions> options,
    ILoggerFactory loggerFactory) : IFfmpegProcessRunnerFactory
{
    public IFfmpegProcessRunner Create()
        => new FfmpegProcessRunner(options, loggerFactory.CreateLogger<FfmpegProcessRunner>());
}
