using Microsoft.Extensions.Options;
using Nagare.Application.Abstractions;
using Nagare.Domain.Common;
using Nagare.Domain.Profiles;

namespace Nagare.Infrastructure.Persistence;

/// <summary>
/// JSON-file-backed <see cref="StreamProfile"/> repository (ADR-0004). Load-all/replace-all
/// at this small volume. Maps aggregates to private storage records (the Domain exposes no
/// setters for deserialization).
/// </summary>
public sealed class JsonStreamProfileRepository : IStreamProfileRepository
{
    private readonly JsonFileStore _store;

    public JsonStreamProfileRepository(IOptions<NagareStorageOptions> options)
        => _store = new JsonFileStore(options.Value.ProfilesFile);

    public async Task<IReadOnlyList<StreamProfile>> GetAllAsync(CancellationToken ct)
    {
        var records = await _store.ReadAllAsync<StreamProfileRecord>(ct);
        return [.. records.Select(ToAggregate)];
    }

    public async Task<StreamProfile?> GetByIdAsync(ProfileId id, CancellationToken ct)
    {
        var all = await GetAllAsync(ct);
        return all.FirstOrDefault(p => p.Id == id);
    }

    public async Task SaveAsync(StreamProfile profile, CancellationToken ct)
    {
        var records = (await _store.ReadAllAsync<StreamProfileRecord>(ct)).ToList();
        var index = records.FindIndex(r => r.Id == profile.Id.Value);
        var record = ToRecord(profile);

        if (index >= 0)
            records[index] = record;
        else
            records.Add(record);

        await _store.WriteAllAsync(records, ct);
    }

    public async Task DeleteAsync(ProfileId id, CancellationToken ct)
    {
        var records = (await _store.ReadAllAsync<StreamProfileRecord>(ct)).ToList();
        if (records.RemoveAll(r => r.Id == id.Value) > 0)
            await _store.WriteAllAsync(records, ct);
    }

    private static StreamProfileRecord ToRecord(StreamProfile p)
    {
        var v = p.Video;
        var a = p.Audio;
        var i = p.Input;
        return new StreamProfileRecord(
            p.Id.Value,
            p.Name,
            new EncodingRecord(v.Codec, v.Preset, v.RateControl, v.BitrateKbps, v.MaxrateKbps, v.BufsizeKbps,
                v.GopSize, v.KeyintMin, v.Resolution?.Width, v.Resolution?.Height, v.Fps),
            new AudioRecord(a.Codec, a.BitrateKbps, a.SampleRateHz),
            new InputRecord(i.ReadAtNativeRate, i.LoopInfinitely));
    }

    private static StreamProfile ToAggregate(StreamProfileRecord r)
    {
        Resolution? resolution = r.Video.ResolutionWidth is { } w && r.Video.ResolutionHeight is { } h
            ? new Resolution(w, h)
            : null;

        var video = new EncodingSettings(
            r.Video.Codec, r.Video.Preset, r.Video.RateControl,
            r.Video.BitrateKbps, r.Video.MaxrateKbps, r.Video.BufsizeKbps,
            r.Video.GopSize, r.Video.KeyintMin, resolution, r.Video.Fps);

        var audio = new AudioSettings(r.Audio.Codec, r.Audio.BitrateKbps, r.Audio.SampleRateHz);
        var input = new InputOptions(r.Input.ReadAtNativeRate, r.Input.LoopInfinitely);

        return StreamProfile.Restore(new ProfileId(r.Id), r.Name, video, audio, input);
    }

    // Private storage schema — decoupled from the aggregate.
    private sealed record StreamProfileRecord(
        Guid Id,
        string Name,
        EncodingRecord Video,
        AudioRecord Audio,
        InputRecord Input);

    private sealed record EncodingRecord(
        VideoCodec Codec,
        string Preset,
        RateControl RateControl,
        int BitrateKbps,
        int MaxrateKbps,
        int BufsizeKbps,
        int GopSize,
        int KeyintMin,
        int? ResolutionWidth,
        int? ResolutionHeight,
        int? Fps);

    private sealed record AudioRecord(AudioCodec Codec, int BitrateKbps, int SampleRateHz);

    private sealed record InputRecord(bool ReadAtNativeRate, bool LoopInfinitely);
}
