using System.Text.Json;

namespace Nagare.Infrastructure.Persistence;

/// <summary>
/// Reads/writes a JSON collection with atomic write (temp file + File.Replace) and a
/// per-file application lock (SemaphoreSlim) (ADR-0004, ARCHITECTURE.md §6.4). One instance
/// per file path (keyed via the DI factory).
/// </summary>
public sealed class JsonFileStore(string filePath)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyList<T>> ReadAllAsync<T>(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(filePath))
                return [];

            await using var stream = File.OpenRead(filePath);
            if (stream.Length == 0)
                return [];

            var items = await JsonSerializer.DeserializeAsync<List<T>>(stream, SerializerOptions, ct);
            return items ?? [];
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WriteAllAsync<T>(IReadOnlyList<T> items, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var tempFile = filePath + ".tmp";

            await using (var stream = File.Create(tempFile))
            {
                await JsonSerializer.SerializeAsync(stream, items, SerializerOptions, ct);
            }

            // Atomic replace: File.Replace requires the destination to exist.
            if (File.Exists(filePath))
                File.Replace(tempFile, filePath, destinationBackupFileName: null);
            else
                File.Move(tempFile, filePath);
        }
        finally
        {
            _gate.Release();
        }
    }
}
