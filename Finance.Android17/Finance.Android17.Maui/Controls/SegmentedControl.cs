using System.Collections.Specialized;
using System.Windows.Input;

namespace Finance.Android17.Maui;

/// <summary>
/// Choix exclusif entre quelques valeurs, présenté en segments (période de l'accueil, thème). Le segment choisi est
/// un bouton clair sur un fond creusé, comme dans la maquette. <see cref="SelectedItem"/> se lie en lecture seule :
/// le choix part par <see cref="Command"/> avec la valeur en paramètre, et le ViewModel renvoie la sélection.
/// </summary>
public sealed class SegmentedControl : Border
{
    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items), typeof(IEnumerable<string>), typeof(SegmentedControl), null, propertyChanged: OnItemsChanged);

    public static readonly BindableProperty IconsProperty = BindableProperty.Create(
        nameof(Icons), typeof(string), typeof(SegmentedControl), string.Empty, propertyChanged: (b, _, _) => ((SegmentedControl)b).Rebuild());

    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(
        nameof(SelectedItem), typeof(string), typeof(SegmentedControl), string.Empty, propertyChanged: (b, _, _) => ((SegmentedControl)b).UpdateSelection());

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SegmentedControl));

    private readonly Grid _grid = new() { ColumnSpacing = 2 };
    private readonly List<Segment> _segments = [];

    public SegmentedControl()
    {
        Padding = 3;
        StrokeThickness = 0;
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 };
        AppColors.Bind(this, BackgroundColorProperty, "ToggleBackground");
        Content = _grid;
    }

    public IEnumerable<string>? Items { get => (IEnumerable<string>?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <summary>Noms d'icônes séparés par des virgules, dans l'ordre des valeurs (facultatif).</summary>
    public string Icons { get => (string)GetValue(IconsProperty); set => SetValue(IconsProperty, value); }

    public string SelectedItem { get => (string)GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }

    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    private static void OnItemsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (SegmentedControl)bindable;
        if (oldValue is INotifyCollectionChanged previous) previous.CollectionChanged -= control.OnItemsCollectionChanged;
        if (newValue is INotifyCollectionChanged current) current.CollectionChanged += control.OnItemsCollectionChanged;
        control.Rebuild();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        _grid.Children.Clear();
        _grid.ColumnDefinitions.Clear();
        _segments.Clear();

        var items = Items?.ToList() ?? [];
        var icons = Icons.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < items.Count; i++)
        {
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var segment = new Segment(items[i], i < icons.Length ? icons[i] : null);
            segment.Tapped += OnSegmentTapped;
            Grid.SetColumn(segment.Host, i);
            _grid.Children.Add(segment.Host);
            _segments.Add(segment);
        }
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        foreach (var segment in _segments) segment.Apply(segment.Item == SelectedItem);
    }

    private void OnSegmentTapped(object? sender, string item)
    {
        if (Command is { } command)
        {
            if (command.CanExecute(item)) command.Execute(item);
        }
        else
        {
            SelectedItem = item;
        }
    }

    private sealed class Segment
    {
        private readonly Label _label;
        private readonly IconView? _icon;

        public Segment(string item, string? icon)
        {
            Item = item;
            _label = new Label { Text = item, FontSize = 13, VerticalOptions = LayoutOptions.Center };
            var row = new HorizontalStackLayout { Spacing = 7, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            if (icon is not null)
            {
                _icon = new IconView { Icon = icon, WidthRequest = 16, HeightRequest = 16 };
                row.Children.Add(_icon);
            }
            row.Children.Add(_label);

            Host = new Border
            {
                Content = row,
                Padding = new Thickness(6, 0),
                HeightRequest = 42,
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 9 }
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => Tapped?.Invoke(this, Item);
            Host.GestureRecognizers.Add(tap);
        }

        public event EventHandler<string>? Tapped;

        public string Item { get; }
        public Border Host { get; }

        public void Apply(bool selected)
        {
            if (selected) AppColors.Bind(Host, BackgroundColorProperty, "Surface");
            else Host.BackgroundColor = Colors.Transparent;

            var tint = selected ? "Primary" : "TextSecondary";
            AppColors.Bind(_label, Label.TextColorProperty, tint);
            _label.FontFamily = selected ? "OpenSansSemibold" : "OpenSansRegular";
            if (_icon is not null) _icon.SetAppThemeColor(IconView.IconColorProperty, AppColors.Variant(tint, false), AppColors.Variant(tint, true));

            SemanticProperties.SetDescription(Host, selected ? $"{Item}, sélectionné" : Item);
        }
    }
}
