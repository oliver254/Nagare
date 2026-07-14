using Nagare.Domain.Common;
using Nagare.Domain.Profiles;

namespace Nagare.Application.Profiles;

/// <summary>Read model of a <see cref="StreamProfile"/> for the UI (ARCHITECTURE.md §3.2).</summary>
public sealed record StreamProfileDto(
    ProfileId Id,
    string Name,
    EncodingSettings Video,
    AudioSettings Audio,
    InputOptions Input)
{
    public static StreamProfileDto From(StreamProfile profile)
        => new(profile.Id, profile.Name, profile.Video, profile.Audio, profile.Input);
}
