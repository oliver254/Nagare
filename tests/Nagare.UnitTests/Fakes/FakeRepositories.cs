using Nagare.Application.Abstractions;
using Nagare.Domain.Channels;
using Nagare.Domain.Common;
using Nagare.Domain.Profiles;

namespace Nagare.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IStreamProfileRepository"/> (no mock framework). Only the lookup used by
/// the coordinator is implemented; the write side throws, so an unexpected call is loud.
/// </summary>
public sealed class FakeStreamProfileRepository : IStreamProfileRepository
{
    private readonly Dictionary<ProfileId, StreamProfile> _profiles = [];

    public void Add(StreamProfile profile) => _profiles[profile.Id] = profile;

    public Task<StreamProfile?> GetByIdAsync(ProfileId id, CancellationToken ct)
        => Task.FromResult(_profiles.GetValueOrDefault(id));

    public Task<IReadOnlyList<StreamProfile>> GetAllAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<StreamProfile>>([.. _profiles.Values]);

    public Task SaveAsync(StreamProfile profile, CancellationToken ct) => throw new NotSupportedException();

    public Task DeleteAsync(ProfileId id, CancellationToken ct) => throw new NotSupportedException();
}

/// <summary>In-memory <see cref="IChannelRepository"/>. See <see cref="FakeStreamProfileRepository"/>.</summary>
public sealed class FakeChannelRepository : IChannelRepository
{
    private readonly Dictionary<ChannelId, Channel> _channels = [];

    public void Add(Channel channel) => _channels[channel.Id] = channel;

    public Task<Channel?> GetByIdAsync(ChannelId id, CancellationToken ct)
        => Task.FromResult(_channels.GetValueOrDefault(id));

    public Task<IReadOnlyList<Channel>> GetAllAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Channel>>([.. _channels.Values]);

    public Task SaveAsync(Channel channel, CancellationToken ct) => throw new NotSupportedException();

    public Task DeleteAsync(ChannelId id, CancellationToken ct) => throw new NotSupportedException();
}

/// <summary>
/// <see cref="IFfmpegCommandBuilder"/> returning a fixed, harmless command: the real mapping is
/// already covered by the golden tests of FfmpegCommandBuilder — here it is just a payload.
/// </summary>
public sealed class FakeFfmpegCommandBuilder : IFfmpegCommandBuilder
{
    public int BuildCallCount { get; private set; }

    public FfmpegCommand Build(StreamProfile profile, Channel channel, string inputFilePath)
    {
        BuildCallCount++;
        return new FfmpegCommand(
            ["-i", inputFilePath, "-f", "flv", "rtmp://example.invalid/app/key"],
            $"-i {inputFilePath} -f flv rtmp://example.invalid/app/****",
            ["key"]);
    }
}
