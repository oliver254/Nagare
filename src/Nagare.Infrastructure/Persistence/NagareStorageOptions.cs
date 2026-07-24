namespace Nagare.Infrastructure.Persistence;

/// <summary>
/// Storage location for JSON files and the Data Protection keyring (ADR-0004, ADR-0005).
/// Defaults to %APPDATA%\Nagare.
/// </summary>
public sealed class NagareStorageOptions
{
    public const string SectionName = "Nagare:Storage";

    /// <summary>Root directory; empty -> %APPDATA%\Nagare.</summary>
    public string RootDirectory { get; set; } = string.Empty;

    public string ResolvedRoot => string.IsNullOrWhiteSpace(RootDirectory)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nagare")
        : RootDirectory;

    public string ProfilesFile => Path.Combine(ResolvedRoot, "profiles.json");
    public string ChannelsFile => Path.Combine(ResolvedRoot, "targets.json");

    /// <summary>User-written application configuration — ffmpeg paths only (ADR-0010).</summary>
    public string SettingsFile => Path.Combine(ResolvedRoot, "settings.json");

    public string KeyringDirectory => Path.Combine(ResolvedRoot, "keys");
}
