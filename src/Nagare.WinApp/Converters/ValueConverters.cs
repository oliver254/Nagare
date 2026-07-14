using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Nagare.Domain.Profiles;

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
/// Health indicator (SPEC §6): the warning must be VISUALLY distinct, not a word among words.
/// Falls back to plain colours if the Fluent theme brushes cannot be resolved.
/// </summary>
public sealed partial class HealthToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var warning = value is true;
        var key = warning ? "SystemFillColorCautionBrush" : "SystemFillColorSuccessBrush";

        // Fully qualified: inside Nagare.WinApp.*, a bare "Application" resolves to the
        // Nagare.Application NAMESPACE, not to the WinUI type.
        var resources = Microsoft.UI.Xaml.Application.Current.Resources;

        if (resources.TryGetValue(key, out var brush) && brush is Brush themed)
            return themed;

        return new SolidColorBrush(warning ? Colors.Orange : Colors.Green);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
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

/// <summary>Formats a live stat with its unit: "30 fps", "1,02x", "12 drops".</summary>
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

        return isSpeed ? $"{number}x" : $"{number} {unit}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
