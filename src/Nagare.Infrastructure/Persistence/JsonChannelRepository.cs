using Microsoft.Extensions.Options;
using Nagare.Application.Abstractions;
using Nagare.Domain.Channels;
using Nagare.Domain.Common;

namespace Nagare.Infrastructure.Persistence;

/// <summary>
/// JSON-file-backed <see cref="Channel"/> repository (ADR-0004). The key is stored encrypted
/// (<see cref="ProtectedStreamKey.CipherText"/>, ADR-0005): a readable JSON leaks nothing.
/// </summary>
public sealed class JsonChannelRepository : IChannelRepository
{
    private readonly JsonFileStore _store;

    public JsonChannelRepository(IOptions<NagareStorageOptions> options)
        => _store = new JsonFileStore(options.Value.ChannelsFile);

    public async Task<IReadOnlyList<Channel>> GetAllAsync(CancellationToken ct)
    {
        var records = await _store.ReadAllAsync<ChannelRecord>(ct);
        return [.. records.Select(ToAggregate)];
    }

    public async Task<Channel?> GetByIdAsync(ChannelId id, CancellationToken ct)
    {
        var all = await GetAllAsync(ct);
        return all.FirstOrDefault(c => c.Id == id);
    }

    public async Task SaveAsync(Channel channel, CancellationToken ct)
    {
        var records = (await _store.ReadAllAsync<ChannelRecord>(ct)).ToList();
        var index = records.FindIndex(r => r.Id == channel.Id.Value);
        var record = ToRecord(channel);

        if (index >= 0)
            records[index] = record;
        else
            records.Add(record);

        await _store.WriteAllAsync(records, ct);
    }

    public async Task DeleteAsync(ChannelId id, CancellationToken ct)
    {
        var records = (await _store.ReadAllAsync<ChannelRecord>(ct)).ToList();
        if (records.RemoveAll(r => r.Id == id.Value) > 0)
            await _store.WriteAllAsync(records, ct);
    }

    private static ChannelRecord ToRecord(Channel c)
        => new(c.Id.Value, c.Name, c.Platform, c.BaseUrl, c.Key.CipherText);

    private static Channel ToAggregate(ChannelRecord r)
        => Channel.Restore(new ChannelId(r.Id), r.Name, r.Platform, r.BaseUrl, new ProtectedStreamKey(r.KeyCipherText));

    // Private storage schema — the key is encrypted at rest.
    private sealed record ChannelRecord(
        Guid Id,
        string Name,
        Platform Platform,
        string BaseUrl,
        string KeyCipherText);
}
