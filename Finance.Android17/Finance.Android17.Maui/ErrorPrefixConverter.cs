using System.Globalization;

namespace Finance.Android17.Maui;

/// <summary>Préfixe un message d'erreur non vide par « Erreur : » pour qu'il ne repose pas sur la couleur seule.</summary>
public sealed class ErrorPrefixConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string { Length: > 0 } text ? $"Erreur : {text}" : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
