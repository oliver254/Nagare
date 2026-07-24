using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nagare.Application.Abstractions;

namespace Nagare.Infrastructure.Persistence;

/// <summary>
/// <c>%APPDATA%\Nagare\settings.json</c> (ADR-0010). Same write discipline as
/// <see cref="JsonFileStore"/> — application lock plus temp-file-then-replace — but a SINGLE
/// object rather than a collection, so it cannot reuse it: <see cref="JsonFileStore"/>
/// serializes a <c>List&lt;T&gt;</c>, and this file is one record.
///
/// <para>Plaintext file, paths only. No stream key ever enters it (ADR-0005).</para>
/// </summary>
public sealed class JsonFfmpegSettingsStore : IFfmpegSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<JsonFfmpegSettingsStore> _logger;

    public JsonFfmpegSettingsStore(
        IOptions<NagareStorageOptions> options,
        ILogger<JsonFfmpegSettingsStore> logger)
    {
        SettingsFilePath = options.Value.SettingsFile;
        _logger = logger;
    }

    public string SettingsFilePath { get; }

    public async Task<FfmpegPathSettings?> LoadAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(SettingsFilePath))
                return null;

            await using var stream = File.OpenRead(SettingsFilePath);
            if (stream.Length == 0)
                return null;

            var record = await JsonSerializer.DeserializeAsync<SettingsRecord>(stream, SerializerOptions, ct);
            if (record is null)
                return null;

            return new FfmpegPathSettings(record.ExecutablePath ?? string.Empty, record.FfprobePath ?? string.Empty);
        }
        catch (JsonException ex)
        {
            // A file we cannot parse must READ as "nothing configured", not as a startup crash:
            // the settings screen is exactly where the user would go to repair it (ADR-0010).
            _logger.LogWarning(ex, "Ignoring the malformed settings file {SettingsFilePath}.", SettingsFilePath);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(FfmpegPathSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _gate.WaitAsync(ct);
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var tempFile = SettingsFilePath + ".tmp";
            var record = new SettingsRecord(settings.ExecutablePath, settings.FfprobePath);

            await using (var stream = File.Create(tempFile))
            {
                await JsonSerializer.SerializeAsync(stream, record, SerializerOptions, ct);
            }

            // Atomic replace: File.Replace requires the destination to exist.
            if (File.Exists(SettingsFilePath))
                File.Replace(tempFile, SettingsFilePath, destinationBackupFileName: null);
            else
                File.Move(tempFile, SettingsFilePath);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Private storage schema. <see cref="FfmpegPathSettings"/> is not serialized directly: its
    /// Resolved* members are computed properties, which System.Text.Json would happily write out
    /// as two extra fields that mean nothing on disk and would be read back as noise.
    /// </summary>
    private sealed record SettingsRecord(string? ExecutablePath, string? FfprobePath);
}
