using Nagare.Domain.Common;

namespace Nagare.Domain.Channels;

/// <summary>Streaming channel: Twitch, YouTube or custom RTMP (ARCHITECTURE.md §2.3).</summary>
public sealed class Channel : AggregateRoot
{
    public ChannelId Id { get; }
    public string Name { get; private set; }
    public Platform Platform { get; private set; }

    /// <summary>Invariant: rtmp:// or rtmps:// scheme.</summary>
    public string BaseUrl { get; private set; }

    /// <summary>NEVER the plaintext (ADR-0005).</summary>
    public ProtectedStreamKey Key { get; private set; }

    private Channel(ChannelId id, string name, Platform platform, string baseUrl, ProtectedStreamKey key)
    {
        Id = id;
        Name = name;
        Platform = platform;
        BaseUrl = baseUrl;
        Key = key;
    }

    public static Channel Create(string name, Platform platform, string baseUrl, ProtectedStreamKey key)
        => new(ChannelId.New(), ValidateName(name), platform, ValidateBaseUrl(baseUrl), RequireKey(key));

    /// <summary>Rehydration from persistence — revalidates invariants (ADR-0004).</summary>
    public static Channel Restore(ChannelId id, string name, Platform platform, string baseUrl, ProtectedStreamKey key)
        => new(id, ValidateName(name), platform, ValidateBaseUrl(baseUrl), RequireKey(key));

    public void Update(string name, Platform platform, string baseUrl)
    {
        Name = ValidateName(name);
        Platform = platform;
        BaseUrl = ValidateBaseUrl(baseUrl);
    }

    public void ReplaceKey(ProtectedStreamKey newKey) => Key = RequireKey(newKey);

    private static string ValidateName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException("Channel name cannot be empty.");
        return trimmed;
    }

    private static string ValidateBaseUrl(string? baseUrl)
    {
        var trimmed = baseUrl?.Trim();
        if (string.IsNullOrEmpty(trimmed)
            || (!trimmed.StartsWith("rtmp://", StringComparison.OrdinalIgnoreCase)
                && !trimmed.StartsWith("rtmps://", StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException("Base URL must use the rtmp:// or rtmps:// scheme.");
        }

        return trimmed;
    }

    private static ProtectedStreamKey RequireKey(ProtectedStreamKey? key)
        => key ?? throw new DomainException("A protected stream key is required.");
}
