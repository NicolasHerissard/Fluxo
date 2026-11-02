using System.Collections.Specialized;
using Finance.Android17.Presentation;

namespace Finance.Android17.Maui;

/// <summary>Anneau de répartition (les catégories de dépenses de l'accueil), tracé du haut vers la droite, sans trou à remplir : l'intérieur reste transparent.</summary>
public sealed class DonutChart : GraphicsView
{
    public static readonly BindableProperty SlicesProperty = BindableProperty.Create(
        nameof(Slices), typeof(IEnumerable<DonutSlice>), typeof(DonutChart), null, propertyChanged: OnSlicesChanged);

    public static readonly BindableProperty ThicknessProperty = BindableProperty.Create(
        nameof(Thickness), typeof(double), typeof(DonutChart), 16d, propertyChanged: (b, _, _) => ((DonutChart)b).Invalidate());

    public DonutChart()
    {
        Drawable = new DonutDrawable(this);
        InputTransparent = true;
    }

    public IEnumerable<DonutSlice>? Slices { get => (IEnumerable<DonutSlice>?)GetValue(SlicesProperty); set => SetValue(SlicesProperty, value); }

    public double Thickness { get => (double)GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app is null) return;
        app.RequestedThemeChanged -= OnThemeChanged;
        if (Handler is not null) app.RequestedThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => Invalidate();

    private static void OnSlicesChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var chart = (DonutChart)bindable;
        if (oldValue is INotifyCollectionChanged previous) previous.CollectionChanged -= chart.OnCollectionChanged;
        if (newValue is INotifyCollectionChanged current) current.CollectionChanged += chart.OnCollectionChanged;
        chart.Invalidate();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Invalidate();

    private sealed class DonutDrawable(DonutChart owner) : IDrawable
    {
        private const float StepDegrees = 2f;
        private const float GapDegrees = 1.4f;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var thickness = (float)owner.Thickness;
            var diameter = Math.Min(dirtyRect.Width, dirtyRect.Height) - thickness;
            if (diameter <= 0) return;
            var centerX = dirtyRect.Center.X;
            var centerY = dirtyRect.Center.Y;
            var radius = diameter / 2f;

            canvas.StrokeSize = thickness;
            canvas.StrokeLineCap = LineCap.Butt;
            canvas.StrokeLineJoin = LineJoin.Round;
            canvas.Antialias = true;

            var slices = owner.Slices?.Where(x => x.Share > 0).ToList() ?? [];
            if (slices.Count == 0)
            {
                canvas.StrokeColor = AppColors.Resolve("Track");
                canvas.DrawCircle(centerX, centerY, radius);
                return;
            }

            var total = slices.Sum(x => x.Share);
            var start = 0d;
            foreach (var slice in slices)
            {
                var sweep = slice.Share / total * 360d;
                var gap = slices.Count > 1 && sweep > GapDegrees * 2 ? GapDegrees : 0;
                canvas.StrokeColor = ParseColor(slice.Color);
                canvas.DrawPath(Arc(centerX, centerY, radius, start + gap / 2, sweep - gap));
                start += sweep;
            }
        }

        /// <summary>Arc de cercle approché par de petits segments, à partir de 12 h et dans le sens horaire.</summary>
        private static PathF Arc(float centerX, float centerY, float radius, double startDegrees, double sweepDegrees)
        {
            var path = new PathF();
            var steps = Math.Max(1, (int)Math.Ceiling(sweepDegrees / StepDegrees));
            for (var i = 0; i <= steps; i++)
            {
                var angle = (startDegrees + sweepDegrees * i / steps - 90d) * Math.PI / 180d;
                var x = centerX + radius * (float)Math.Cos(angle);
                var y = centerY + radius * (float)Math.Sin(angle);
                if (i == 0) path.MoveTo(x, y);
                else path.LineTo(x, y);
            }
            return path;
        }

        private static Color ParseColor(string hex)
        {
            try { return Color.FromArgb(hex); }
            catch { return Colors.Gray; }
        }
    }
}
