namespace Finance.Android17.Maui;

/// <summary>
/// Dessine une icône de <see cref="AppIcons"/>. Sans <see cref="IconColor"/>, elle prend la couleur de texte du thème
/// courant et se redessine quand le thème change ; une couleur liée par <c>AppThemeBinding</c> fait de même.
/// </summary>
public sealed class IconView : GraphicsView
{
    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), typeof(string), typeof(IconView), string.Empty, propertyChanged: OnAppearanceChanged);

    public static readonly BindableProperty IconColorProperty = BindableProperty.Create(
        nameof(IconColor), typeof(Color), typeof(IconView), null, propertyChanged: OnAppearanceChanged);

    public static readonly BindableProperty StrokeWidthProperty = BindableProperty.Create(
        nameof(StrokeWidth), typeof(double), typeof(IconView), 1.8, propertyChanged: OnAppearanceChanged);

    public IconView()
    {
        Drawable = new IconDrawable(this);
        WidthRequest = 22;
        HeightRequest = 22;
        HorizontalOptions = LayoutOptions.Center;
        VerticalOptions = LayoutOptions.Center;
        InputTransparent = true;
    }

    /// <summary>Nom de l'icône (voir <see cref="AppIcons"/>).</summary>
    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public Color? IconColor { get => (Color?)GetValue(IconColorProperty); set => SetValue(IconColorProperty, value); }

    /// <summary>Épaisseur du trait sur la grille de 24 × 24 (elle suit la taille de l'icône).</summary>
    public double StrokeWidth { get => (double)GetValue(StrokeWidthProperty); set => SetValue(StrokeWidthProperty, value); }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app is null) return;
        app.RequestedThemeChanged -= OnThemeChanged;
        if (Handler is not null) app.RequestedThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => Invalidate();

    private static void OnAppearanceChanged(BindableObject bindable, object oldValue, object newValue) => ((IconView)bindable).Invalidate();

    private sealed class IconDrawable(IconView owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (AppIcons.Get(owner.Icon) is not { } path) return;

            var scale = Math.Min(dirtyRect.Width, dirtyRect.Height) / 24f;
            canvas.SaveState();
            canvas.Translate(dirtyRect.X + (dirtyRect.Width - 24f * scale) / 2f, dirtyRect.Y + (dirtyRect.Height - 24f * scale) / 2f);
            canvas.Scale(scale, scale);
            canvas.StrokeColor = owner.IconColor ?? AppColors.Resolve("TextPrimary");
            canvas.StrokeSize = (float)owner.StrokeWidth;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;
            canvas.DrawPath(path);
            canvas.RestoreState();
        }
    }
}
