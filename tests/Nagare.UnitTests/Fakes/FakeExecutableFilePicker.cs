using Nagare.ViewModels.Abstractions;

namespace Nagare.UnitTests.Fakes;

/// <summary>Executable picker returning a canned path (or null = user cancelled).</summary>
public sealed class FakeExecutableFilePicker(string? path) : IExecutableFilePicker
{
    public int PickCallCount { get; private set; }

    public Task<string?> PickAsync()
    {
        PickCallCount++;
        return Task.FromResult(path);
    }
}
