using System.Windows.Input;

namespace Finance.Android17.Maui;

/// <summary>Présentation d'un <see cref="IconButton"/>.</summary>
public enum IconButtonStyle
{
    /// <summary>Icône seule (fermer, modifier, supprimer).</summary>
    Plain,
    /// <summary>Icône seule, plus compacte, pour les actions d'une ligne de liste.</summary>
    Small,
    /// <summary>Tuile à bord fin, comme la cloche de l'en-tête.</summary>
    Tile,
    /// <summary>Bouton rond rempli et surélevé : l'action principale d'une page.</summary>
    Fab,
    /// <summary>Carré de la couleur principale, pour une action à côté d'un champ.</summary>
    Filled
}

/// <summary>Bouton d'icône : zone tactile d'au moins 44, icône teintée selon le thème, point de notification facultatif.</summary>
public sealed class IconButton : Border
{
    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), typeof(string), typeof(IconButton), string.Empty, propertyChanged: (b, _, n) => ((IconButton)b)._icon.Icon = (string)n);

    public static readonly BindableProperty ButtonStyleProperty = BindableProperty.Create(
        nameof(ButtonStyle), typeof(IconButtonStyle), typeof(IconButton), IconButtonStyle.Plain, propertyChanged: (b, _, _) => ((IconButton)b).ApplyStyle());

    public static readonly BindableProperty IconColorProperty = BindableProperty.Create(
        nameof(IconColor), typeof(Color), typeof(IconButton), null, propertyChanged: (b, _, _) => ((IconButton)b).ApplyStyle());

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(IconButton));

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(IconButton));

    public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(
        nameof(Description), typeof(string), typeof(IconButton), string.Empty, propertyChanged: (b, _, n) => SemanticProperties.SetDescription((IconButton)b, (string)n));

    public static readonly BindableProperty HasBadgeProperty = BindableProperty.Create(
        nameof(HasBadge), typeof(bool), typeof(IconButton), false, propertyChanged: (b, _, n) => ((IconButton)b)._badge.IsVisible = (bool)n);

    private readonly IconView _icon = new();
    private readonly Border _badge;

    public IconButton()
    {
        _badge = new Border
        {
            WidthRequest = 9,
            HeightRequest = 9,
            Padding = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 5 },
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 9, 9, 0),
            IsVisible = false,
            InputTransparent = true
        };
        AppColors.Bind(_badge, BackgroundColorProperty, "NotificationDot");
        AppColors.BindBrush(_badge, Border.StrokeProperty, "Surface");

        var content = new Grid();
        content.Children.Add(_icon);
        content.Children.Add(_badge);
        Content = content;

        var tap = new TapGestureRecognizer();
        tap.Tapped += OnTapped;
        GestureRecognizers.Add(tap);
        SemanticProperties.SetHint(this, "Bouton");
        ApplyStyle();
    }

    public event EventHandler? Clicked;

    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public IconButtonStyle ButtonStyle { get => (IconButtonStyle)GetValue(ButtonStyleProperty); set => SetValue(ButtonStyleProperty, value); }
    public Color? IconColor { get => (Color?)GetValue(IconColorProperty); set => SetValue(IconColorProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    /// <summary>Libellé lu par les lecteurs d'écran : obligatoire, l'icône seule ne dit rien.</summary>
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>Affiche le point de notification en haut à droite.</summary>
    public bool HasBadge { get => (bool)GetValue(HasBadgeProperty); set => SetValue(HasBadgeProperty, value); }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (!IsEnabled) return;
        if (Command is { } command && !command.CanExecute(CommandParameter)) return;

        _ = PulseAsync();
        Clicked?.Invoke(this, EventArgs.Empty);
        Command?.Execute(CommandParameter);
    }

    /// <summary>Retour visuel bref : une zone tactile sans effet d'ondulation ne dit pas qu'elle a réagi.</summary>
    private async Task PulseAsync()
    {
        await this.FadeToAsync(0.55, 60);
        await this.FadeToAsync(1, 120);
    }

    private void ApplyStyle()
    {
        var (size, radius, iconSize) = ButtonStyle switch
        {
            IconButtonStyle.Tile => (44d, 12d, 20d),
            IconButtonStyle.Fab => (58d, 29d, 26d),
            IconButtonStyle.Filled => (52d, 12d, 22d),
            IconButtonStyle.Small => (40d, 20d, 19d),
            _ => (46d, 23d, 22d)
        };
        WidthRequest = size;
        HeightRequest = size;
        Padding = 0;
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = radius };
        _icon.WidthRequest = iconSize;
        _icon.HeightRequest = iconSize;
        ClearValue(ShadowProperty);

        switch (ButtonStyle)
        {
            case IconButtonStyle.Tile:
                StrokeThickness = 1;
                AppColors.Bind(this, BackgroundColorProperty, "Surface");
                AppColors.BindBrush(this, StrokeProperty, "Outline");
                _icon.IconColor = IconColor;
                break;
            case IconButtonStyle.Fab:
                StrokeThickness = 0;
                AppColors.Bind(this, BackgroundColorProperty, "Primary");
                _icon.SetAppThemeColor(IconView.IconColorProperty, AppColors.Variant("OnPrimary", false), AppColors.Variant("OnPrimary", true));
                _icon.StrokeWidth = 2.2;
                Shadow = new Shadow { Brush = Brush.Black, Opacity = 0.3f, Offset = new Point(0, 5), Radius = 12 };
                break;
            case IconButtonStyle.Filled:
                StrokeThickness = 0;
                AppColors.Bind(this, BackgroundColorProperty, "Primary");
                _icon.SetAppThemeColor(IconView.IconColorProperty, AppColors.Variant("OnPrimary", false), AppColors.Variant("OnPrimary", true));
                break;
            default:
                StrokeThickness = 0;
                BackgroundColor = Colors.Transparent;
                _icon.IconColor = IconColor;
                break;
        }
    }
}
