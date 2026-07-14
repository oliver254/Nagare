using Nagare.Domain.Common;
using Nagare.Domain.Sessions;

namespace Nagare.UnitTests.Domain.Sessions;

/// <summary>Invariants and exponential backoff of the reconnection policy (ARCHITECTURE.md §2.4).</summary>
public sealed class ReconnectPolicyTests
{
    private static readonly TimeSpan TwoSeconds = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan OneMinute = TimeSpan.FromSeconds(60);

    [Fact]
    public void Constructor_ValidValues_KeepsThem()
    {
        var policy = new ReconnectPolicy(5, TwoSeconds, 2.0, OneMinute);

        Assert.Equal(5, policy.MaxAttempts);
        Assert.Equal(TwoSeconds, policy.InitialDelay);
        Assert.Equal(2.0, policy.Factor);
        Assert.Equal(OneMinute, policy.MaxDelay);
    }

    [Fact]
    public void Default_Policy_IsFiveAttemptsWithTwoSecondsDoubling()
    {
        var policy = ReconnectPolicy.Default;

        Assert.Equal(5, policy.MaxAttempts);
        Assert.Equal(TwoSeconds, policy.InitialDelay);
        Assert.Equal(2.0, policy.Factor);
        Assert.Equal(OneMinute, policy.MaxDelay);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveMaxAttempts_ThrowsDomainException(int maxAttempts)
        => Assert.Throws<DomainException>(() => new ReconnectPolicy(maxAttempts, TwoSeconds, 2.0, OneMinute));

    [Fact]
    public void Constructor_NonPositiveInitialDelay_ThrowsDomainException()
        => Assert.Throws<DomainException>(() => new ReconnectPolicy(5, TimeSpan.Zero, 2.0, OneMinute));

    [Fact]
    public void Constructor_FactorBelowOne_ThrowsDomainException()
        => Assert.Throws<DomainException>(() => new ReconnectPolicy(5, TwoSeconds, 0.9, OneMinute));

    [Fact]
    public void Constructor_MaxDelayBelowInitialDelay_ThrowsDomainException()
        => Assert.Throws<DomainException>(() => new ReconnectPolicy(5, OneMinute, 2.0, TwoSeconds));

    [Theory]
    [InlineData(1, 2)]     // initial delay
    [InlineData(2, 4)]     // x factor
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    [InlineData(5, 32)]
    [InlineData(6, 60)]    // capped at MaxDelay
    [InlineData(20, 60)]
    public void DelayFor_ExponentialBackoff_IsCappedAtMaxDelay(int attempt, int expectedSeconds)
    {
        var policy = new ReconnectPolicy(5, TwoSeconds, 2.0, OneMinute);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), policy.DelayFor(attempt));
    }

    [Fact]
    public void DelayFor_FactorOfOne_KeepsAConstantDelay()
    {
        var policy = new ReconnectPolicy(3, TwoSeconds, 1.0, OneMinute);

        Assert.Equal(TwoSeconds, policy.DelayFor(1));
        Assert.Equal(TwoSeconds, policy.DelayFor(3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DelayFor_NonPositiveAttempt_ThrowsDomainException(int attempt)
        => Assert.Throws<DomainException>(() => ReconnectPolicy.Default.DelayFor(attempt));
}
