namespace Finance.Android17.Maui;

/// <summary>
/// Lit les couleurs de <c>Colors.xaml</c> depuis le code (contrôles dessinés ou construits à la main). Une couleur de
/// thème porte le nom d'une paire : <c>"Primary"</c> désigne <c>PrimaryLight</c> et <c>PrimaryDark</c>.
/// </summary>
internal static class AppColors
{
    public static bool IsDark => Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark;

    /// <summary>La variante du thème courant de la paire <paramref name="name"/>.</summary>
    public static Color Resolve(string name) => Variant(name, IsDark);

    public static Color Variant(string name, bool dark) => Find(name + (dark ? "Dark" : "Light"));

    /// <summary>Associe la paire <paramref name="name"/> à une propriété : elle suit alors les changements de thème.</summary>
    public static void Bind(BindableObject target, BindableProperty property, string name) =>
        target.SetAppThemeColor(property, Variant(name, dark: false), Variant(name, dark: true));

    /// <summary>Comme <see cref="Bind"/>, pour une propriété de type <see cref="Brush"/> (le trait d'une <see cref="Border"/>).</summary>
    public static void BindBrush(BindableObject target, BindableProperty property, string name) =>
        target.SetAppTheme<Brush>(property, new SolidColorBrush(Variant(name, dark: false)), new SolidColorBrush(Variant(name, dark: true)));

    private static Color Find(string key) =>
        Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Colors.Magenta;
}
