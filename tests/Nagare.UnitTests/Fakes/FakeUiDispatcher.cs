using Nagare.Presentation.Abstractions;

namespace Nagare.UnitTests.Fakes;

/// <summary>
/// Runs the marshalled work INLINE — there is no UI thread in a unit test — and counts the posts.
/// The count is what proves the throttle: 10 stats snapshots, 1 post.
/// </summary>
public sealed class FakeUiDispatcher : IUiDispatcher
{
    /// <summary>When false, simulates a closed window: Post refuses the work.</summary>
    public bool IsAlive { get; set; } = true;

    public int PostCount { get; private set; }

    public bool Post(Action action)
    {
        if (!IsAlive)
            return false;

        PostCount++;
        action();
        return true;
    }
}
