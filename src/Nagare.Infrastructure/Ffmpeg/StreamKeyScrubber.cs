using Nagare.Domain.Channels;

namespace Nagare.Infrastructure.Ffmpeg;

/// <summary>
/// Transverse security rule (ARCHITECTURE.md §6.3). ffmpeg repeats the output URL (key
/// included) in its stderr messages; every process line is passed through this scrubber
/// (each secret value replaced by ****) before being emitted, buffered, logged or stored
/// as an error. No other component ever sees an unscrubbed line.
/// </summary>
public sealed class StreamKeyScrubber
{
    private readonly IReadOnlyList<string> _secrets;

    public StreamKeyScrubber(IReadOnlyList<string> secrets)
    {
        // Keep only non-empty secrets, longest first so a longer secret containing a
        // shorter one is masked before the shorter substring match runs.
        _secrets = [.. secrets
            .Where(static s => !string.IsNullOrEmpty(s))
            .Distinct()
            .OrderByDescending(static s => s.Length)];
    }

    public string Scrub(string line)
    {
        if (string.IsNullOrEmpty(line) || _secrets.Count == 0)
            return line;

        var result = line;
        foreach (var secret in _secrets)
            result = result.Replace(secret, ProtectedStreamKey.Mask, StringComparison.Ordinal);

        return result;
    }
}
