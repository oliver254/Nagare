using Nagare.Domain.Profiles;

namespace Nagare.UnitTests.Domain.Profiles;

public sealed class InputOptionsTests
{
    [Fact]
    public void Default_BusinessDefault_ReadsAtNativeRateAndLoopsInfinitely()
    {
        // The spec command line starts with "-re -stream_loop -1": both flags on by default.
        Assert.Equal(new InputOptions(ReadAtNativeRate: true, LoopInfinitely: true), InputOptions.Default);
    }
}
