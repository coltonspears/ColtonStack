using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ColtonStack.Client.Extensions.Pokemon;

/// <summary>Type name → the community-standard type color. Presentation-only, stateless, extension-local.</summary>
public sealed class PokemonTypeBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> Brushes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Normal"] = Freeze("#A8A77A"), ["Fire"] = Freeze("#EE8130"), ["Water"] = Freeze("#6390F0"),
        ["Electric"] = Freeze("#F7D02C"), ["Grass"] = Freeze("#7AC74C"), ["Ice"] = Freeze("#96D9D6"),
        ["Fighting"] = Freeze("#C22E28"), ["Poison"] = Freeze("#A33EA1"), ["Ground"] = Freeze("#E2BF65"),
        ["Flying"] = Freeze("#A98FF3"), ["Psychic"] = Freeze("#F95587"), ["Bug"] = Freeze("#A6B91A"),
        ["Rock"] = Freeze("#B6A136"), ["Ghost"] = Freeze("#735797"), ["Dragon"] = Freeze("#6F35FC"),
        ["Dark"] = Freeze("#705746"), ["Steel"] = Freeze("#B7B7CE"), ["Fairy"] = Freeze("#D685AD"),
    };

    private static readonly SolidColorBrush Fallback = Freeze("#68A090");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string type && Brushes.TryGetValue(type, out var brush) ? brush : Fallback;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;

    private static SolidColorBrush Freeze(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
