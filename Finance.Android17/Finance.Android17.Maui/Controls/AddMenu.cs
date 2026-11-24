namespace Finance.Android17.Maui;

/// <summary>
/// Bouton d'action flottant qui se déplie : « + » devient « × » et deux actions (ajouter une transaction, scanner un ticket)
/// sortent au-dessus, avec leur libellé, sur un fond voilé. Se place dans la cellule de la page où se trouvait le bouton « + ».
/// </summary>
public sealed class AddMenu : Grid
{
    public static readonly BindableProperty FabMarginProperty = BindableProperty.Create(
        nameof(FabMargin), typeof(Thickness), typeof(AddMenu), new Thickness(0, 0, 20, 24), propertyChanged: (b, _, n) => ((AddMenu)b)._stack.Margin = (Thickness)n);

    private const string ClosedDescription = "Ajouter une transaction ou scanner un ticket";
    private const string OpenDescription = "Fermer le menu";

    private readonly BoxView _scrim;
    private readonly VerticalStackLayout _stack;
    private readonly IconButton _fab;
    private readonly List<View> _rows = [];
    private bool _isOpen;
    private bool _isAnimating;

    public AddMenu()
    {
        // La grille elle-même laisse passer les touches vers la page ; seuls le voile, les actions et le bouton les captent.
        InputTransparent = true;
        CascadeInputTransparent = false;

        _scrim = new BoxView { Color = Color.FromArgb("#99000000"), IsVisible = false, Opacity = 0 };
        var closeTap = new TapGestureRecognizer();
        closeTap.Tapped += (_, _) => _ = SetOpenAsync(false);
        _scrim.GestureRecognizers.Add(closeTap);
        SemanticProperties.SetDescription(_scrim, OpenDescription);

        _stack = new VerticalStackLayout { Spacing = 12, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End, Margin = FabMargin };
        _stack.Children.Add(BuildRow("Scanner un ticket", "camera", () => ScanRequested?.Invoke(this, EventArgs.Empty)));
        _stack.Children.Add(BuildRow("Ajouter une transaction", "pencil", () => AddRequested?.Invoke(this, EventArgs.Empty)));

        _fab = new IconButton { Icon = "plus", ButtonStyle = IconButtonStyle.Fab, HorizontalOptions = LayoutOptions.End, Description = ClosedDescription };
        _fab.Clicked += (_, _) => _ = SetOpenAsync(!_isOpen);
        _stack.Children.Add(_fab);

        Children.Add(_scrim);
        Children.Add(_stack);
    }

    public event EventHandler? AddRequested;
    public event EventHandler? ScanRequested;

    /// <summary>Marge du bouton « + » dans sa cellule.</summary>
    public Thickness FabMargin { get => (Thickness)GetValue(FabMarginProperty); set => SetValue(FabMarginProperty, value); }

    private View BuildRow(string text, string icon, Action raise)
    {
        var label = new Label { Text = text, FontFamily = "OpenSansSemibold", FontSize = 15, VerticalOptions = LayoutOptions.Center, InputTransparent = true };
        AppColors.Bind(label, Label.TextColorProperty, "TextPrimary");

        var pill = new Border
        {
            Padding = new Thickness(16, 11),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Content = label,
            VerticalOptions = LayoutOptions.Center,
            Shadow = new Shadow { Brush = Brush.Black, Opacity = 0.15f, Offset = new Point(0, 3), Radius = 8 }
        };
        AppColors.Bind(pill, BackgroundColorProperty, "Surface");
        AppColors.BindBrush(pill, Border.StrokeProperty, "Outline");

        // La ligne entière est la zone tactile : l'icône ne capte pas le toucher elle-même.
        var tile = new IconButton { Icon = icon, ButtonStyle = IconButtonStyle.Tile, InputTransparent = true };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 10, HorizontalOptions = LayoutOptions.End, IsVisible = false, Opacity = 0 };
        row.Add(pill, 0);
        row.Add(tile, 1);
        SemanticProperties.SetDescription(row, text);

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (!_isOpen) return;
            raise();
            _ = SetOpenAsync(false);
        };
        row.GestureRecognizers.Add(tap);
        _rows.Add(row);
        return row;
    }

    private async Task SetOpenAsync(bool open)
    {
        if (_isAnimating || open == _isOpen) return;
        _isAnimating = true;
        _isOpen = open;
        _fab.Description = open ? OpenDescription : ClosedDescription;

        const uint duration = 200;
        // Les actions les plus proches du bouton apparaissent en premier à l'ouverture, et disparaissent en dernier à la fermeture.
        var ordered = Enumerable.Reverse(_rows).ToList();

        if (open)
        {
            _scrim.IsVisible = true;
            foreach (var row in _rows) { row.IsVisible = true; row.Opacity = 0; row.TranslationY = 18; }
            _fab.Icon = "x";
            _fab.Rotation = -90;

            var tasks = new List<Task>
            {
                _scrim.FadeToAsync(1, duration, Easing.CubicOut),
                _fab.RotateToAsync(0, duration, Easing.CubicOut)
            };
            for (var i = 0; i < ordered.Count; i++) tasks.Add(ShowRowAsync(ordered[i], i * 45));
            await Task.WhenAll(tasks);
        }
        else
        {
            _fab.Icon = "plus";
            _fab.Rotation = 90;

            var tasks = new List<Task>
            {
                _scrim.FadeToAsync(0, duration, Easing.CubicIn),
                _fab.RotateToAsync(0, duration, Easing.CubicOut)
            };
            foreach (var row in _rows) tasks.Add(HideRowAsync(row));
            await Task.WhenAll(tasks);
            _scrim.IsVisible = false;
            foreach (var row in _rows) row.IsVisible = false;
        }

        _isAnimating = false;
    }

    private static async Task ShowRowAsync(View row, int delay)
    {
        if (delay > 0) await Task.Delay(delay);
        await Task.WhenAll(row.FadeToAsync(1, 180, Easing.CubicOut), row.TranslateToAsync(0, 0, 180, Easing.CubicOut));
    }

    private static Task HideRowAsync(View row) =>
        Task.WhenAll(row.FadeToAsync(0, 140, Easing.CubicIn), row.TranslateToAsync(0, 18, 140, Easing.CubicIn));
}
