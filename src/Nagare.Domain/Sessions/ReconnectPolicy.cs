using Nagare.Domain.Common;

namespace Nagare.Domain.Sessions;

/// <summary>Reconnection policy with exponential backoff (ARCHITECTURE.md §2.4).</summary>
public sealed record ReconnectPolicy
{
    public int MaxAttempts { get; }
    public TimeSpan InitialDelay { get; }
    public double Factor { get; }
    public TimeSpan MaxDelay { get; }

    public ReconnectPolicy(int maxAttempts, TimeSpan initialDelay, double factor, TimeSpan maxDelay)
    {
        if (maxAttempts <= 0)
            throw new DomainException("The maximum number of attempts must be strictly positive.");
        if (initialDelay <= TimeSpan.Zero)
            throw new DomainException("The initial delay must be strictly positive.");
        if (factor < 1.0)
            throw new DomainException("The backoff factor must be greater than or equal to 1.");
        if (maxDelay < initialDelay)
            throw new DomainException("The maximum delay must be greater than or equal to the initial delay.");

        MaxAttempts = maxAttempts;
        InitialDelay = initialDelay;
        Factor = factor;
        MaxDelay = maxDelay;
    }

    public static ReconnectPolicy Default => new(5, TimeSpan.FromSeconds(2), 2.0, TimeSpan.FromSeconds(60));

    /// <summary>Delay before attempt n (1-based): min(InitialDelay × Factor^(n-1), MaxDelay).</summary>
    public TimeSpan DelayFor(int attempt)
    {
        if (attempt <= 0)
            throw new DomainException("The attempt number must be greater than or equal to 1.");

        var delayMs = InitialDelay.TotalMilliseconds * Math.Pow(Factor, attempt - 1);
        return TimeSpan.FromMilliseconds(Math.Min(delayMs, MaxDelay.TotalMilliseconds));
    }
}
