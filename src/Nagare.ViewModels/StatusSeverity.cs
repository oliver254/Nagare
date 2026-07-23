namespace Nagare.ViewModels;

/// <summary>
/// How loud a session state should read on screen. It is a TRANSLATION of
/// <see cref="Nagare.Domain.Sessions.SessionStatus"/> plus the health indicator — not a rule, and
/// deliberately not a colour: mapping it to a brush is the view's job (Nagare.WinApp/Converters),
/// which is what keeps this project free of any WinUI type.
/// </summary>
public enum StatusSeverity
{
    /// <summary>Nothing is happening: no session, or a session that ended normally.</summary>
    Neutral,

    /// <summary>Something is under way and nothing is wrong yet (Starting).</summary>
    Information,

    /// <summary>On air and healthy.</summary>
    Success,

    /// <summary>On air but degraded: reconnecting, or speed below 1.0x / growing drops.</summary>
    Caution,

    /// <summary>The session failed.</summary>
    Critical
}
