using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Nagare.Domain.Profiles;
using Nagare.Domain.Sessions;
using Nagare.ViewModels;

namespace Nagare.WinApp.Converters;

/// <summary>Null / empty -> false. Drives the InfoBars: a message exists, therefore it is shown.</summary>
public sealed partial class NotNullToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is string text ? !string.IsNullOrWhiteSpace(text) : value is not null;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Health indicator (SPEC §6) applied to a live statistic: an unhealthy value is coloured, a healthy
/// one is not. Colouring "everything is fine" in green too would spend the eye's attention on the
/// normal case — and by the Von Restorff effect, make the anomaly harder to spot, not easier.
///
/// <para>The colour NEVER carries the meaning on its own: the badge next to these figures always
/// spells the state out in words (accessibility §9).</para>
/// </summary>
public sealed partial class HealthToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => ThemeBrush(
            value is true ? "SystemFillColorCautionBrush" : "TextFillColorPrimaryBrush",
            value is true ? Colors.Orange : Colors.Gray);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    /// <summary>
    /// Resolves a Fluent theme brush by key, with a plain colour as a last resort. Fully qualified:
    /// inside Nagare.WinApp.*, a bare "Application" resolves to the Nagare.Application NAMESPACE,
    /// not to the WinUI type.
    /// </summary>
    internal static Brush ThemeBrush(string key, Windows.UI.Color fallback)
        => Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out var brush) && brush is Brush themed
            ? themed
            : new SolidColorBrush(fallback);
}

/// <summary>bool -> Visibility. x:Bind does not convert it on its own.</summary>
public sealed partial class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility.Visible;
}

/// <summary>
/// The opposite. Two states that exclude each other — Démarrer / Arrêter, checklist / health — share
/// one place on screen and are swapped by this pair, so the destructive action is never sitting next
/// to the primary one (Fitts).
/// </summary>
public sealed partial class InvertedBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility.Collapsed;
}

/// <summary>Null / empty -> collapsed. Keeps an empty message from reserving a hole in the layout.</summary>
public sealed partial class NotNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var present = value is string text ? !string.IsNullOrWhiteSpace(text) : value is not null;
        return present ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>Negates a flag, for the controls a live session must freeze.</summary>
public sealed partial class NotConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is not true;
}

/// <summary>
/// <see cref="StatusSeverity"/> -> a Fluent semantic brush. Pass <c>Background</c> as the converter
/// parameter for the soft fill of a badge, anything else for the foreground/stroke.
///
/// This is the ONLY place where a session state becomes a colour: the ViewModel stays free of any
/// WinUI type, and every colour comes from the theme — none is hard-coded, so light, dark and high
/// contrast follow the system on their own.
/// </summary>
public sealed partial class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var background = parameter as string == "Background";

        var (key, fallback) = value switch
        {
            StatusSeverity.Information => (Semantic("Attention", background), Colors.SteelBlue),
            StatusSeverity.Success => (Semantic("Success", background), Colors.Green),
            StatusSeverity.Caution => (Semantic("Caution", background), Colors.Orange),
            StatusSeverity.Critical => (Semantic("Critical", background), Colors.Red),
            _ => (background ? "LayerFillColorDefaultBrush" : "TextFillColorSecondaryBrush", Colors.Gray)
        };

        return HealthToBrushConverter.ThemeBrush(key, fallback);
    }

    private static string Semantic(string name, bool background)
        => background ? $"SystemFillColor{name}BackgroundBrush" : $"SystemFillColor{name}Brush";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary><see cref="StatusSeverity"/> -> the severity of the InfoBar carrying the end-of-session report.</summary>
public sealed partial class SeverityToInfoBarConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        StatusSeverity.Success => InfoBarSeverity.Success,
        StatusSeverity.Caution => InfoBarSeverity.Warning,
        StatusSeverity.Critical => InfoBarSeverity.Error,
        _ => InfoBarSeverity.Informational
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// True while a session is warming up or trying to come back — the two states where a spinner is the
/// honest signal that something is happening (Doherty). Running and Stopped are steady: no motion.
/// </summary>
public sealed partial class StatusToBusyRingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var spinning = value is SessionStatus.Starting or SessionStatus.Reconnecting;

        // Pass "Visibility" to collapse the ring instead of merely stopping it: an inactive
        // ProgressRing still holds its place and would leave a hole next to the status word.
        return parameter as string == "Visibility"
            ? spinning ? Visibility.Visible : Visibility.Collapsed
            : spinning;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// <see cref="Nagare.Application.Channels.ChannelDto.KeyConfigured"/> -> a word. This is ALL the UI
/// will ever know about a key: whether there is one (ADR-0005).
/// </summary>
public sealed partial class KeyStateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? "Clé configurée" : "Aucune clé";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>Anything -> its text. x:Bind requires a string when the target is a string.</summary>
public sealed partial class AsTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value?.ToString() ?? string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>One-line summary of a profile's video settings, for the list.</summary>
public sealed partial class EncodingSummaryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not EncodingSettings video)
            return string.Empty;

        var resolution = video.Resolution is { } r ? $" · {r.Width}×{r.Height}" : string.Empty;
        var fps = video.Fps is { } f ? $" · {f} fps" : string.Empty;

        return $"{video.Codec} · {video.Preset} · {video.RateControl} · {video.BitrateKbps} kbps{resolution}{fps}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Formats a live stat with its unit: "30 fps", "1,02x" — or the bare number when no unit is given,
/// which is how the stat tiles show a count whose meaning is already in the label under it.
/// </summary>
public sealed partial class StatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var unit = parameter as string ?? string.Empty;
        var isSpeed = unit == "x";

        var number = value switch
        {
            double d => d.ToString(isSpeed ? "0.00" : "0.#", CultureInfo.CurrentCulture),
            int i => i.ToString(CultureInfo.CurrentCulture),
            _ => value?.ToString() ?? "-"
        };

        return isSpeed ? $"{number}x" : $"{number} {unit}".TrimEnd();
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
