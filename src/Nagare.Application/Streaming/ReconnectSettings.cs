using Nagare.Domain.Sessions;

namespace Nagare.Application.Streaming;

/// <summary>
/// Default reconnection policy read from configuration
/// (Nagare:Reconnect:*, ARCHITECTURE.md §5). Mutable POCO for options binding.
/// </summary>
public sealed class ReconnectSettings
{
    public const string SectionName = "Nagare:Reconnect";

    public int MaxAttempts { get; set; } = 5;
    public double InitialDelaySeconds { get; set; } = 2;
    public double Factor { get; set; } = 2;
    public double MaxDelaySeconds { get; set; } = 60;

    public ReconnectPolicy ToPolicy() => new(
        MaxAttempts,
        TimeSpan.FromSeconds(InitialDelaySeconds),
        Factor,
        TimeSpan.FromSeconds(MaxDelaySeconds));
}
