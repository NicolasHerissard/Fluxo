namespace Finance.Android17.Maui;

/// <summary>
/// Pastille carrée à icône teintée, dont la couleur suit le ton demandé et le thème : <c>primary</c>, <c>success</c>,
/// <c>danger</c>, <c>warning</c>, <c>violet</c>, <c>blue</c> ou <c>neutral</c>. Les gravités des notifications
/// (<c>info</c>, <c>warning</c>, <c>critical</c>) sont acceptées telles quelles.
/// </summary>
public sealed class IconTile : Border
{
    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), typeof(string), typeof(IconTile), string.Empty, propertyChanged: (b, _, n) => ((IconTile)b)._icon.Icon = (string)n);

    public static readonly BindableProperty ToneProperty = BindableProperty.Create(
        nameof(Tone), typeof(string), typeof(IconTile), "primary", propertyChanged: (b, _, _) => ((IconTile)b).ApplyTone());

    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size), typeof(double), typeof(IconTile), 40d, propertyChanged: (b, _, _) => ((IconTile)b).ApplySize());

    private readonly IconView _icon = new();

    public IconTile()
    {
        Padding = 0;
        StrokeThickness = 0;
        Content = _icon;
        InputTransparent = true;
        ApplySize();
        ApplyTone();
    }

    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public string Tone { get => (string)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>Côté du carré ; l'icône occupe un peu moins de la moitié.</summary>
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    private void ApplySize()
    {
        WidthRequest = Size;
        HeightRequest = Size;
        VerticalOptions = LayoutOptions.Center;
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = Math.Round(Size * 0.3) };
        _icon.WidthRequest = Math.Round(Size * 0.5);
        _icon.HeightRequest = Math.Round(Size * 0.5);
    }

    private void ApplyTone()
    {
        var (background, ink) = Tone switch
        {
            "success" => ("SuccessSoft", "SuccessInk"),
            "danger" or "critical" => ("ExpenseSoft", "ExpenseInk"),
            "warning" => ("WarningSoft", "WarningInk"),
            "violet" => ("VioletSoft", "VioletInk"),
            "blue" or "info" => ("BlueSoft", "BlueInk"),
            "neutral" => ("SurfaceContainer", "TextSecondary"),
            _ => ("PrimaryContainer", "Primary")
        };
        AppColors.Bind(this, BackgroundColorProperty, background);
        _icon.SetAppThemeColor(IconView.IconColorProperty, AppColors.Variant(ink, dark: false), AppColors.Variant(ink, dark: true));
    }
}
