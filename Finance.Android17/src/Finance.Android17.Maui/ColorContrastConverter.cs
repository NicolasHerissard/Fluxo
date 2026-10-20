using System.Globalization;

namespace Finance.Android17.Maui;

public sealed class ColorContrastConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            var color = Color.FromArgb(value?.ToString() ?? "#6750A4");
            var luminance = 0.2126 * Linear(color.Red) + 0.7152 * Linear(color.Green) + 0.0722 * Linear(color.Blue);
            return luminance > 0.45 ? Colors.Black : Colors.White;
        }
        catch { return Colors.White; }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    private static double Linear(float value) => value <= 0.03928f ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
}
