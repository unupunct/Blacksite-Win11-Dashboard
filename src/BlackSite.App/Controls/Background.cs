using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using BlackSite.App.Services;

namespace BlackSite.App.Controls;

/// <summary>Static backdrop: atmosphere, digital grid and circuit traces. Redrawn only on resize / theme / style change.</summary>
public sealed class BackgroundCanvas : FrameworkElement
{
    public static readonly DependencyProperty StyleNameProperty = DependencyProperty.Register(
        nameof(StyleName), typeof(string), typeof(BackgroundCanvas),
        new FrameworkPropertyMetadata("Circuit", FrameworkPropertyMetadataOptions.AffectsRender));

    public string StyleName { get => (string)GetValue(StyleNameProperty); set => SetValue(StyleNameProperty, value); }

    public BackgroundCanvas()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => ThemeManager.Changed += OnTheme;
        Unloaded += (_, _) => ThemeManager.Changed -= OnTheme;
    }

    private void OnTheme() => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 10 || h < 10) return;
        var accent = Hud.Accent;
        dc.DrawRectangle(Hud.Brush((Color)ColorConverter.ConvertFromString("#040810")), null, new Rect(0, 0, w, h));

        if (StyleName != "Plain")
        {
            var glow = new RadialGradientBrush { Center = new Point(0.5, 0.15), GradientOrigin = new Point(0.5, 0.15), RadiusX = 0.75, RadiusY = 0.9 };
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(0x38, 0x10, 0x50, 0xC0), 0));
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(0x10, 0x00, 0x30, 0x90), 0.55));
            glow.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            glow.Freeze();
            dc.DrawRectangle(glow, null, new Rect(0, 0, w, h));

            var gridPen = new Pen(Hud.Brush(accent, 0x12), 1);
            gridPen.Freeze();
            const double cell = 48;
            for (double x = 0; x < w; x += cell) dc.DrawLine(gridPen, new Point(x + .5, 0), new Point(x + .5, h));
            for (double y = 0; y < h; y += cell) dc.DrawLine(gridPen, new Point(0, y + .5), new Point(w, y + .5));
        }

        if (StyleName == "Circuit") DrawCircuits(dc, w, h, accent);

        var vig = new RadialGradientBrush { RadiusX = 0.8, RadiusY = 0.8 };
        vig.GradientStops.Add(new GradientStop(Colors.Transparent, 0.55));
        vig.GradientStops.Add(new GradientStop(Color.FromArgb(0xC0, 0, 0, 0), 1));
        vig.Freeze();
        dc.DrawRectangle(vig, null, new Rect(0, 0, w, h));
    }

    private static void DrawCircuits(DrawingContext dc, double w, double h, Color accent)
    {
        var rnd = new Random(7); // fixed seed: the layout is stable between runs
        var pen = new Pen(Hud.Brush(accent, 0x30), 1.2) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        var nodeBrush = Hud.Brush(accent, 0x55);
        const double g = 24;
        for (int n = 0; n < 46; n++)
        {
            double x = Math.Round(rnd.NextDouble() * w / g) * g, y = Math.Round(rnd.NextDouble() * h / g) * g;
            // bias traces towards the screen edges so the centre stays readable
            if (rnd.NextDouble() < 0.7) x = rnd.NextDouble() < 0.5 ? Math.Round(rnd.NextDouble() * 0.18 * w / g) * g : w - Math.Round(rnd.NextDouble() * 0.18 * w / g) * g;
            var geo = new StreamGeometry();
            Point last;
            using (var ctx = geo.Open())
            {
                last = new Point(x, y);
                ctx.BeginFigure(last, false, false);
                bool horiz = rnd.Next(2) == 0;
                int segs = rnd.Next(2, 6);
                for (int s = 0; s < segs; s++)
                {
                    double len = (rnd.Next(2, 7)) * g * (rnd.Next(2) == 0 ? 1 : -1);
                    last = horiz ? new Point(Math.Clamp(last.X + len, 0, w), last.Y) : new Point(last.X, Math.Clamp(last.Y + len, 0, h));
                    ctx.LineTo(last, true, false);
                    if (rnd.Next(3) == 0) // 45° chamfer
                    {
                        var d = new Point(last.X + (horiz ? 0 : g) * Math.Sign(len), last.Y + (horiz ? g : 0) * Math.Sign(len));
                        ctx.LineTo(d, true, false);
                        last = d;
                    }
                    horiz = !horiz;
                }
            }
            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
            dc.DrawEllipse(null, pen, last, 3.5, 3.5);
            dc.DrawEllipse(nodeBrush, null, new Point(x, y), 2, 2);
        }
    }
}

/// <summary>Faint drifting particles. Ticks at ~25 fps and only while animations are enabled.</summary>
public sealed class ParticleLayer : FrameworkElement
{
    private sealed class P { public double X, Y, Vy, Vx, R, A; }

    private readonly List<P> _ps = new();
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly Random _rnd = new();
    private string _style = "Circuit";

    public ParticleLayer()
    {
        IsHitTestVisible = false;
        _timer.Tick += (_, _) => Step();
        Loaded += (_, _) => { AnimationManager.Changed += Refresh; Refresh(); };
        Unloaded += (_, _) => { AnimationManager.Changed -= Refresh; _timer.Stop(); };
    }

    public string StyleName { get => _style; set { _style = value; Refresh(); } }

    public void Refresh()
    {
        bool on;
        on = AnimationManager.Enabled && AnimationManager.Intensity > 0.01 && (_style == "Particles" || _style == "Circuit");
        int target = on ? (int)((_style == "Particles" ? 40 : 14) + AnimationManager.Intensity * (_style == "Particles" ? 90 : 30)) : 0;
        while (_ps.Count > target) _ps.RemoveAt(_ps.Count - 1);
        while (_ps.Count < target) _ps.Add(New(true));
        if (on && !_timer.IsEnabled) _timer.Start();
        if (!on) { _timer.Stop(); InvalidateVisual(); }
    }

    private P New(bool anywhere) => new()
    {
        X = _rnd.NextDouble(), Y = anywhere ? _rnd.NextDouble() : 1.02,
        Vy = -(0.0004 + _rnd.NextDouble() * 0.0012), Vx = (_rnd.NextDouble() - 0.5) * 0.0004,
        R = 0.7 + _rnd.NextDouble() * 1.6, A = 0.15 + _rnd.NextDouble() * 0.5,
    };

    private void Step()
    {
        double k = 0.5 + AnimationManager.Intensity;
        for (int i = 0; i < _ps.Count; i++)
        {
            var p = _ps[i];
            p.Y += p.Vy * k; p.X += p.Vx * k;
            if (p.Y < -0.02 || p.X < -0.02 || p.X > 1.02) _ps[i] = New(false);
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        var c = Hud.Accent;
        foreach (var p in _ps)
            dc.DrawEllipse(Hud.Brush(c, (byte)(p.A * 255)), null, new Point(p.X * w, p.Y * h), p.R, p.R);
    }
}
