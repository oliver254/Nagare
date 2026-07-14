using Nagare.Domain.Common;

namespace Nagare.Domain.Profiles;

/// <summary>
/// Named, reusable and persisted encoding profile. Aggregate root without child
/// entities: three immutable value objects (ARCHITECTURE.md §2.2).
/// </summary>
public sealed class StreamProfile : AggregateRoot
{
    public ProfileId Id { get; }
    public string Name { get; private set; }
    public EncodingSettings Video { get; private set; }
    public AudioSettings Audio { get; private set; }
    public InputOptions Input { get; private set; }

    private StreamProfile(ProfileId id, string name, EncodingSettings video, AudioSettings audio, InputOptions input)
    {
        Id = id;
        Name = name;
        Video = video;
        Audio = audio;
        Input = input;
    }

    public static StreamProfile Create(string name, EncodingSettings video, AudioSettings audio, InputOptions input)
        => new(ProfileId.New(), ValidateName(name), Require(video), Require(audio), Require(input));

    /// <summary>Rehydration from persistence — revalidates invariants, no public setters (ADR-0004).</summary>
    public static StreamProfile Restore(ProfileId id, string name, EncodingSettings video, AudioSettings audio, InputOptions input)
        => new(id, ValidateName(name), Require(video), Require(audio), Require(input));

    public void Update(string name, EncodingSettings video, AudioSettings audio, InputOptions input)
    {
        Name = ValidateName(name);
        Video = Require(video);
        Audio = Require(audio);
        Input = Require(input);
    }

    private static string ValidateName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException("Profile name cannot be empty.");
        return trimmed;
    }

    private static T Require<T>(T value) where T : class
        => value ?? throw new DomainException("Profile settings cannot be null.");
}
