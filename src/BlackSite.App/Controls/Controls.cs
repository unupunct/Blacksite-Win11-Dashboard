using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BlackSite.App.Services;

namespace BlackSite.App.Controls;

internal static class Hud
{
    public static Color Accent => ThemeManager.Accent;
    public static readonly Color Green = (Color)ColorConverter.ConvertFromString("#00FF9C");
    public static readonly Color Orange = (Color)ColorConverter.ConvertFromString("#FFA726");
    public static readonly Color Red = (Color)ColorConverter.ConvertFromString("#FF3B5C");
    public static readonly Typeface Face = new(new FontFamily("Bahnschrift, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    public static Color Level(double pct) => pct >= 90 ? Red : pct >= 75 ? Orange : Accent;
    public static Brush Brush(Color c, byte a = 255) { var b = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B)); b.Freeze(); return b; }
}

/// <summary>Circular arc gauge (0-100) with an animated sweep and a centre read-out.</summary>
public sealed class CircularGauge : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(CircularGauge), new PropertyMetadata(0.0, OnValueChanged));
    public static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(
        nameof(DisplayValue), typeof(double), typeof(CircularGauge), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption), typeof(string), typeof(CircularGauge), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double DisplayValue { get => (double)GetValue(DisplayValueProperty); set => SetValue(DisplayValueProperty, value); }
    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    public CircularGauge()
    {
        Loaded += (_, _) => ThemeManager.Changed += OnTheme;
        Unloaded += (_, _) => ThemeManager.Changed -= OnTheme;
    }

    private void OnTheme() => InvalidateVisual();

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var g = (CircularGauge)d;
        var target = Math.Clamp((double)e.NewValue, 0, 100);
        if (AnimationManager.Enabled && g.IsLoaded)
            g.BeginAnimation(DisplayValueProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(500)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        else
        {
            g.BeginAnimation(DisplayValueProperty, null);
            g.DisplayValue = target;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size < 20) return;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        double thick = Math.Max(6, size * 0.09), r = size / 2 - thick;
        var accent = Hud.Accent;
        var valColor = Hud.Level(DisplayValue);

        dc.DrawEllipse(null, new Pen(Hud.Brush(accent, 0x30), thick), c, r, r);
        // tick ring
        var tickPen = new Pen(Hud.Brush(accent, 0x55), 1);
        for (int i = 0; i < 60; i++)
        {
            double a = i * 6 * Math.PI / 180, r1 = r - thick * 0.8, r2 = r1 - (i % 5 == 0 ? 7 : 3);
            dc.DrawLine(tickPen, new Point(c.X + r1 * Math.Sin(a), c.Y - r1 * Math.Cos(a)), new Point(c.X + r2 * Math.Sin(a), c.Y - r2 * Math.Cos(a)));
        }

        double sweep = Math.Clamp(DisplayValue, 0, 100) / 100.0 * 359.9;
        if (sweep > 0.5)
        {
            double a1 = sweep * Math.PI / 180;
            var start = new Point(c.X, c.Y - r);
            var end = new Point(c.X + r * Math.Sin(a1), c.Y - r * Math.Cos(a1));
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(start, false, false);
                ctx.ArcTo(end, new Size(r, r), 0, sweep > 180, SweepDirection.Clockwise, true, false);
            }
            geo.Freeze();
            dc.DrawGeometry(null, new Pen(Hud.Brush(valColor, 0x40), thick + 6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, geo);
            dc.DrawGeometry(null, new Pen(Hud.Brush(valColor), thick * 0.7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, geo);
        }

        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var big = new FormattedText($"{DisplayValue:0}%", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Hud.Face, size * 0.22, Brushes.White, dpi);
        dc.DrawText(big, new Point(c.X - big.Width / 2, c.Y - big.Height / 2 - (string.IsNullOrEmpty(Caption) ? 0 : size * 0.05)));
        if (!string.IsNullOrEmpty(Caption))
        {
            var cap = new FormattedText(Caption, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Hud.Face, size * 0.09, Hud.Brush(accent, 0xCC), dpi);
            dc.DrawText(cap, new Point(c.X - cap.Width / 2, c.Y + size * 0.1));
        }
    }
}

/// <summary>Scrolling area graph. Feed it the full history each tick; it slides smoothly between ticks.</summary>
public sealed class SparklineGraph : FrameworkElement
{
    public static readonly DependencyProperty ShiftProperty = DependencyProperty.Register(
        nameof(Shift), typeof(double), typeof(SparklineGraph), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(SparklineGraph), new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty LineColorProperty = DependencyProperty.Register(
        nameof(LineColor), typeof(Color?), typeof(SparklineGraph), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private double[] _a = Array.Empty<double>();
    private double[] _b = Array.Empty<double>();
    private double _autoMax = 1;

    public double Shift { get => (double)GetValue(ShiftProperty); set => SetValue(ShiftProperty, value); }
    /// <summary>Fixed scale maximum; 0 = auto-scale (min 1).</summary>
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public Color? LineColor { get => (Color?)GetValue(LineColorProperty); set => SetValue(LineColorProperty, value); }
    /// <summary>Optional second series drawn dimmer (e.g. upload next to download).</summary>
    public int Capacity { get; set; } = 90;

    public SparklineGraph()
    {
        ClipToBounds = true;
        Loaded += (_, _) => ThemeManager.Changed += OnTheme;
        Unloaded += (_, _) => ThemeManager.Changed -= OnTheme;
    }

    private void OnTheme() => InvalidateVisual();

    public void SetValues(IReadOnlyList<double> primary, IReadOnlyList<double>? secondary = null)
    {
        _a = primary.TakeLast(Capacity).ToArray();
        _b = secondary?.TakeLast(Capacity).ToArray() ?? Array.Empty<double>();
        if (Maximum <= 0) _autoMax = Math.Max(1, Math.Max(_a.DefaultIfEmpty(0).Max(), _b.DefaultIfEmpty(0).Max()) * 1.2);
        if (AnimationManager.Enabled && IsLoaded)
            BeginAnimation(ShiftProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(1000)));
        else
        {
            BeginAnimation(ShiftProperty, null);
            Shift = 0;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 10 || h < 10) return;
        var accent = LineColor ?? Hud.Accent;
        var grid = new Pen(Hud.Brush(Hud.Accent, 0x22), 1);
        for (int i = 1; i < 4; i++) dc.DrawLine(grid, new Point(0, h * i / 4), new Point(w, h * i / 4));

        double max = Maximum > 0 ? Maximum : _autoMax;
        Draw(dc, _b, w, h, max, Hud.Brush(accent, 0x70), false, accent);
        Draw(dc, _a, w, h, max, Hud.Brush(accent), true, accent);
    }

    private void Draw(DrawingContext dc, double[] v, double w, double h, double max, Brush stroke, bool fill, Color accent)
    {
        if (v.Length < 2) return;
        double step = w / (Capacity - 1);
        double x0 = w - (v.Length - 1) * step + Shift * step;
        var pts = new Point[v.Length];
        for (int i = 0; i < v.Length; i++)
            pts[i] = new Point(x0 + i * step, h - 2 - Math.Clamp(v[i] / max, 0, 1) * (h - 4));
        if (fill)
        {
            var area = new StreamGeometry();
            using (var ctx = area.Open())
            {
                ctx.BeginFigure(new Point(pts[0].X, h), true, true);
                ctx.PolyLineTo(pts, false, false);
                ctx.LineTo(new Point(pts[^1].X, h), false, false);
            }
            area.Freeze();
            var gb = new LinearGradientBrush(Color.FromArgb(0x70, accent.R, accent.G, accent.B), Color.FromArgb(0x05, accent.R, accent.G, accent.B), 90);
            gb.Freeze();
            dc.DrawGeometry(gb, null, area);
        }
        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(pts[0], false, false);
            ctx.PolyLineTo(pts.Skip(1).ToArray(), true, true);
        }
        line.Freeze();
        dc.DrawGeometry(null, new Pen(stroke, 1.6) { LineJoin = PenLineJoin.Round }, line);
    }
}
