using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace GodjiVpn.Controls;

/// Новогодняя тема поверх оболочки: снег и качающиеся гирлянды.
/// Активна с 1 ноября по 31 января; вне сезона элемент ничего не рисует и не тикает.
/// Провод качается как гибкая верёвка: контрольная точка провиса плывёт по трём гармоникам,
/// лампочки едут вдоль провода и отстают от него, как маятники.
public sealed class WinterOverlay : Control
{
    private struct Flake
    {
        public double X, Y, Vy, Size, Alpha, Rot, Rs, Ph, Ph2, Ph3, SwayA, SwayF, Drift, A2, F2, Vf, Gust, Depth;
        public int Spr;
        public bool Front;
    }

    private sealed class Bulb { public double T, Px, Tilt, Resp, Wf, Pp; public bool Init; public int ColorIdx; }
    private sealed class Strand
    {
        public double X0, Cx, X2, Y0, Cy, Y2, Sc, A, P1, P2, P3;
        public Bulb[] Bulbs = System.Array.Empty<Bulb>();
    }

    private static readonly Color[] BulbColors =
    {
        Color.FromRgb(0xFF,0x5A,0x4E), Color.FromRgb(0xFF,0xD2,0x57), Color.FromRgb(0x63,0xD6,0x8C),
        Color.FromRgb(0x6A,0xA9,0xFF), Color.FromRgb(0xFF,0x9D,0x5C)
    };
    private static readonly IBrush Wire = new SolidColorBrush(Color.FromRgb(0x2C, 0x36, 0x34));
    private static readonly IBrush Socket = new SolidColorBrush(Color.FromRgb(0x3A, 0x45, 0x42));
    private static readonly IBrush GlassCore = new SolidColorBrush(Color.Parse("#4DFFF8D9"));
    private static readonly IBrush Filament = new SolidColorBrush(Color.Parse("#D9FFF8D9"));
    private static readonly IBrush GlassHi = new SolidColorBrush(Color.Parse("#80FFFFFF"));
    private static readonly IBrush Stem = new SolidColorBrush(Color.FromRgb(0x2C, 0x36, 0x34));

    private readonly Random _rnd = new();
    private readonly List<Flake> _flakes = new();
    private readonly List<Strand> _strands = new();
    private IImage[]? _backSprites, _frontSprites;
    private Color _spriteColor;
    private double _t;
    private DispatcherTimer? _timer;
    private DateTimeOffset _last;

    private const double Tau = Math.PI * 2;

    public WinterOverlay()
    {
        IsHitTestVisible = false;
        AttachedToVisualTree += (_, _) => { DecorChanged += Sync; Sync(); };
        DetachedFromVisualTree += (_, _) => { DecorChanged -= Sync; _timer?.Stop(); _timer = null; };
    }

    /// Сезон: 1 декабря — 31 января.
    public static bool InSeason()
    {
        var m = DateTime.Now.Month;
        return m == 12 || m == 1;
    }

    /// Переключатель «Новогоднее оформление» из настроек.
    public static bool Enabled { get; private set; } = true;

    /// Срабатывает при включении/выключении оформления: элементы обновляются сразу, без перезапуска.
    public static event Action? DecorChanged;

    public static void SetEnabled(bool on)
    {
        if (Enabled == on) return;
        Enabled = on;
        DecorChanged?.Invoke();
    }

    /// Рисовать ли оформление сейчас (сезон и не выключено в настройках).
    public static bool IsActive() => Enabled && InSeason();

    private void Sync()
    {
        if (IsActive()) Start();
        else { _timer?.Stop(); _timer = null; }
        InvalidateVisual();
    }

    private void Start()
    {
        if (_timer != null) return;
        BuildScene();
        _last = DateTimeOffset.Now;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Background, (_, _) => Tick());
        _timer.Start();
    }

    private void Tick()
    {
        var now = DateTimeOffset.Now;
        double dt = (now - _last).TotalSeconds;
        _last = now;
        if (dt <= 0 || dt > 0.1) dt = 0.016;
        _t += dt;
        UpdateGarlands(dt, _t);
        UpdateSnow(dt);
        InvalidateVisual();
    }

    private void BuildScene()
    {
        double w = Math.Max(Bounds.Width, 300), h = Math.Max(Bounds.Height, 300);
        _flakes.Clear();
        for (var i = 0; i < 64; i++) _flakes.Add(MakeFlake(w, h, false));
        for (var i = 0; i < 8; i++) _flakes.Add(MakeFlake(w, h, true));

        _strands.Clear();
        _strands.Add(MakeStrand(w, -4, w / 2, w + 4, 10, 178, 16, 10, 1.0));
        _strands.Add(MakeStrand(w, -4, w / 2, w + 4, 54, 118, 46, 9, 0.92));
    }

    private Strand MakeStrand(double w, double x0, double cx, double x2, double y0, double cy, double y2, int n, double sc)
    {
        var bulbs = new Bulb[n];
        for (var k = 0; k < n; k++)
        {
            bulbs[k] = new Bulb
            {
                T = 0.06 + 0.88 * k / (n - 1),
                Resp = R(0.7, 1.5), Wf = R(0.4, 1.1), Pp = R(0, Tau),
                ColorIdx = _rnd.Next(BulbColors.Length)
            };
        }
        return new Strand
        {
            X0 = x0, Cx = cx, X2 = x2, Y0 = y0, Cy = cy, Y2 = y2, Sc = sc,
            A = w * 0.032 * R(0.8, 1.15),
            P1 = R(0, Tau), P2 = R(0, Tau), P3 = R(0, Tau),
            Bulbs = bulbs
        };
    }

    private Flake MakeFlake(double w, double h, bool front)
    {
        var f = new Flake { X = R(-40, w + 40), Y = R(-30, h), Rot = R(0, Tau), Ph = R(0, Tau), Ph2 = R(0, Tau), Ph3 = R(0, Tau) };
        if (front)
        {
            f.Vy = R(48, 88); f.Size = R(14, 20); f.Spr = _rnd.Next(3);
            f.Alpha = R(0.35, 0.6); f.Rs = R(-0.5, 0.5); f.Depth = 1.25; f.Front = true;
        }
        else
        {
            f.Vy = R(14, 38); f.Size = R(9, 13); f.Spr = _rnd.Next(3);
            f.Alpha = R(0.3, 0.72); f.Rs = R(-0.9, 0.9); f.Depth = 0.7; f.Front = false;
        }
        f.SwayA = R(8, 30); f.SwayF = R(0.2, 0.55); f.Drift = R(-7, 7);
        f.A2 = R(4, 14); f.F2 = R(0.6, 1.8); f.Vf = R(0.25, 0.8); f.Gust = R(0.5, 1.6);
        return f;
    }

    private double R(double a, double b) => a + _rnd.NextDouble() * (b - a);

    private static double Wind(double t)
        => 16 * Math.Sin(t * 0.11 + 1.3) + 9 * Math.Sin(t * 0.31 + 4) + 22 * Math.Sin(t * 0.043) * Math.Sin(t * 0.097);

    private double StrandSway(Strand d, double t)
        => d.A * (0.62 * Math.Sin(Tau / 6.4 * t + d.P1)
                + 0.26 * Math.Sin(Tau / 2.7 * t + d.P2)
                + 0.12 * Math.Sin(Tau / 11.5 * t + d.P3));

    private static double Qp(double t, double a, double c, double b)
    {
        var u = 1 - t;
        return u * u * a + 2 * u * t * c + t * t * b;
    }

    private void UpdateGarlands(double dt, double t)
    {
        foreach (var d in _strands)
        {
            var dx = StrandSway(d, t) * 2.2;
            var dy = 3 * Math.Sin(Tau / 8.3 * t + d.P2);
            foreach (var bl in d.Bulbs)
            {
                var x = Qp(bl.T, d.X0, d.Cx + dx, d.X2);
                var vel = 0.0;
                if (bl.Init && dt > 0) vel = (x - bl.Px) / dt;
                bl.Px = x; bl.Init = true;
                var target = Math.Max(-11, Math.Min(11, -vel * 0.38)) + 1.1 * Math.Sin(t * bl.Wf * Tau + bl.Pp);
                bl.Tilt += (target - bl.Tilt) * Math.Min(1, dt * bl.Resp * 3);
            }
        }
    }

    private void UpdateSnow(double dt)
    {
        var w = Bounds.Width; var h = Bounds.Height;
        if (w < 50) return;
        for (var i = 0; i < _flakes.Count; i++)
        {
            var f = _flakes[i];
            f.Ph += dt;
            f.Ph2 += dt * f.F2 * Tau;
            f.Ph3 += dt * f.Vf * Tau;
            f.X += (Wind(_t) * f.Depth * f.Gust * 0.5 + f.Drift + Math.Sin(f.Ph2) * f.A2 * 0.55) * dt;
            f.Y += f.Vy * (1 + 0.3 * Math.Sin(f.Ph3)) * dt;
            f.Rot += f.Rs * dt;
            if (f.Y > h + 26) { f.Y = -26; f.X = R(-30, w + 30); }
            if (f.X > w + 45) f.X -= w + 90;
            if (f.X < -45) f.X += w + 90;
            _flakes[i] = f;
        }
    }

    private static bool IsLightTheme()
    {
        if (Application.Current is { } app && app.TryFindResource("BgSolidBrush", out var v) && v is ISolidColorBrush b)
            return (0.299 * b.Color.R + 0.587 * b.Color.G + 0.114 * b.Color.B) / 255.0 > 0.5;
        return false;
    }

    public override void Render(DrawingContext dc)
    {
        if (!IsActive() || _flakes.Count == 0) return;
        var w = Bounds.Width; var h = Bounds.Height;
        if (w < 50 || h < 50) return;
        if (_strands.Count == 0) BuildScene();

        var snow = IsLightTheme() ? Color.Parse("#8FA8C4") : Colors.White;
        EnsureSprites(snow);

        foreach (var d in _strands)
        {
            var dx = StrandSway(d, _t) * 2.2;
            var dy = 3 * Math.Sin(Tau / 8.3 * _t + d.P2);

            var wire = new StreamGeometry();
            using (var sg = wire.Open())
            {
                sg.BeginFigure(new Point(d.X0, d.Y0), false);
                sg.QuadraticBezierTo(new Point(d.Cx + dx, d.Cy + dy), new Point(d.X2, d.Y2));
                sg.EndFigure(false);
            }
            dc.DrawGeometry(null, new Pen(Wire, 1.6), wire);

            foreach (var bl in d.Bulbs)
            {
                var x = Qp(bl.T, d.X0, d.Cx + dx, d.X2);
                var y = Qp(bl.T, d.Y0, d.Cy + dy, d.Y2);
                var sc = d.Sc;
                var rad = bl.Tilt * Math.PI / 180.0;
                var m = Matrix.CreateScale(sc, sc)
                      * Matrix.CreateTranslation(x, y)
                      * Matrix.CreateRotation(rad)
                      * Matrix.CreateTranslation(-x, -y);
                using (dc.PushTransform(m))
                {
                    dc.DrawEllipse(Glow(bl.ColorIdx), null, new Point(0, 17), 21, 21);
                    dc.DrawRectangle(Socket, null, new Rect(-3.6, 3, 7.2, 7.4), 2, 2);
                    dc.DrawLine(new Pen(Stem, 1.8), new Point(0, 0), new Point(0, 3.4));
                    dc.DrawLine(new Pen(Socket, 0.8), new Point(-3.6, 4.8), new Point(3.6, 4.8));
                    dc.DrawLine(new Pen(Socket, 0.8), new Point(-3.6, 6.6), new Point(3.6, 6.6));
                    dc.DrawGeometry(Glass(bl.ColorIdx), new Pen(new SolidColorBrush(Color.Parse("#4D000000")), 0.7), GlassGeom());
                    dc.DrawEllipse(GlassCore, null, new Point(0, 17.4), 3.4, 5);
                    dc.DrawLine(new Pen(Filament, 0.9), new Point(-1.8, 14.5), new Point(0, 19));
                    dc.DrawLine(new Pen(Filament, 0.9), new Point(0, 19), new Point(1.8, 14.5));
                    dc.DrawEllipse(GlassHi, null, new Point(-3, 13.6), 1.7, 2.6);
                }
            }
        }

        foreach (var f in _flakes)
        {
            var rx = f.X + Math.Sin(f.Ph * f.SwayF * Tau) * f.SwayA + Math.Sin(f.Ph2 * 0.6 + 1.3) * f.A2;
            var sz = f.Size; // размер квадрата спрайта
            var img = f.Front ? _frontSprites![f.Spr] : _backSprites![f.Spr];
            var rad = f.Rot * Math.PI / 180.0;
            var m = Matrix.CreateTranslation(rx - sz / 2, f.Y - sz / 2)
                  * Matrix.CreateTranslation(rx, f.Y)
                  * Matrix.CreateRotation(rad)
                  * Matrix.CreateTranslation(-rx, -f.Y);
            using (dc.PushTransform(m))
            using (dc.PushOpacity(f.Alpha))
            {
                dc.DrawImage(img, new Rect(0, 0, sz, sz));
            }
        }
    }

    private static readonly Dictionary<int, IBrush> _glassCache = new();
    private static IBrush Glass(int idx)
    {
        if (!_glassCache.TryGetValue(idx, out var b))
        {
            b = new SolidColorBrush(BulbColors[idx]);
            _glassCache[idx] = b;
        }
        return b;
    }

    private static readonly Dictionary<int, IBrush> _glowCache = new();
    private static IBrush Glow(int idx)
    {
        if (!_glowCache.TryGetValue(idx, out var b))
        {
            var c = BulbColors[idx];
            b = new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(217, c.R, c.G, c.B), 0),
                    new GradientStop(Color.FromArgb(82, c.R, c.G, c.B), 0.45),
                    new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1)
                }
            };
            _glowCache[idx] = b;
        }
        return b;
    }

    private static Geometry GlassGeom()
    {
        var g = new StreamGeometry();
        using (var sg = g.Open())
        {
            sg.BeginFigure(new Point(0, 10), true);
            sg.CubicBezierTo(new Point(4.8, 10), new Point(7.8, 13), new Point(7.8, 17));
            sg.CubicBezierTo(new Point(7.8, 22.2), new Point(4.4, 25.2), new Point(0, 25.2));
            sg.CubicBezierTo(new Point(-4.4, 25.2), new Point(-7.8, 22.2), new Point(-7.8, 17));
            sg.CubicBezierTo(new Point(-7.8, 13), new Point(-4.8, 10), new Point(0, 10));
            sg.EndFigure(true);
        }
        return g;
    }

    private void EnsureSprites(Color snow)
    {
        if (_backSprites != null && _spriteColor == snow) return;
        _spriteColor = snow;
        _backSprites = new IImage[3];
        _frontSprites = new IImage[3];
        double[] back = { 7, 6, 5 };
        double[] front = { 30, 26, 22 };
        for (var i = 0; i < 3; i++)
        {
            _backSprites[i] = RenderFlake(back[i], i, snow, false);
            _frontSprites[i] = RenderFlake(front[i], i, snow, true);
        }
    }

    /// Дендрит (0), гексагональная пластина (1), игольчатая звезда (2). Передний план — мягкие двойные штрихи.
    private static IImage RenderFlake(double px, int variant, Color color, bool soft)
    {
        int pad = 8;
        int s = (int)Math.Ceiling(px) + pad;
        var rtb = new RenderTargetBitmap(new PixelSize(s * 2, s * 2));
        using (var g = rtb.CreateDrawingContext())
        {
            var r = px / 2;
            var core = new Pen(new SolidColorBrush(Color.FromArgb(242, color.R, color.G, color.B)), Math.Max(0.7, px * 0.05));
            var soft_pen = new Pen(new SolidColorBrush(Color.FromArgb(70, color.R, color.G, color.B)), Math.Max(1.4, px * 0.14));
            for (var k = 0; k < 6; k++)
            {
                using (g.PushTransform(Matrix.CreateRotation(k * 360.0 / 6.0 * Math.PI / 180.0)))
                {
                    void Arm()
                    {
                        if (variant == 0)
                        {
                            if (soft) g.DrawLine(soft_pen, new Point(0, 0), new Point(0, -r));
                            g.DrawLine(core, new Point(0, 0), new Point(0, -r));
                            double[,] brs = { { 0.42, 0.42 }, { 0.6, 0.3 }, { 0.78, 0.18 } };
                            for (var i = 0; i < 3; i++)
                            {
                                var by = -r * brs[i, 0]; var bl = r * brs[i, 1];
                                if (soft) { g.DrawLine(soft_pen, new Point(0, by), new Point(bl * 0.5, by - bl * 0.87)); g.DrawLine(soft_pen, new Point(0, by), new Point(-bl * 0.5, by - bl * 0.87)); }
                                g.DrawLine(core, new Point(0, by), new Point(bl * 0.5, by - bl * 0.87));
                                g.DrawLine(core, new Point(0, by), new Point(-bl * 0.5, by - bl * 0.87));
                            }
                            g.DrawLine(core, new Point(0, -r * 0.9), new Point(r * 0.12, -r * 0.78));
                            g.DrawLine(core, new Point(0, -r * 0.9), new Point(-r * 0.12, -r * 0.78));
                        }
                        else if (variant == 1)
                        {
                            double[] rf = { 1, 0.62, 0.3 };
                            foreach (var rfi in rf)
                            {
                                for (var i = 0; i < 6; i++)
                                {
                                    var a1 = i * Tau / 6; var a2 = (i + 1) * Tau / 6;
                                    g.DrawLine(core, new Point(Math.Cos(a1) * r * rfi, Math.Sin(a1) * r * rfi),
                                                    new Point(Math.Cos(a2) * r * rfi, Math.Sin(a2) * r * rfi));
                                }
                            }
                            if (soft) g.DrawLine(soft_pen, new Point(0, 0), new Point(0, -r));
                            g.DrawLine(core, new Point(0, 0), new Point(0, -r));
                        }
                        else
                        {
                            g.DrawLine(core, new Point(0, 0), new Point(0, -r));
                            double[] pp = { 0.5, 0.72 };
                            foreach (var p in pp)
                            {
                                var y = -r * p; var l = r * 0.34 * (p / 0.5) * 0.9;
                                g.DrawLine(core, new Point(0, y), new Point(l, y - l * 0.6));
                                g.DrawLine(core, new Point(0, y), new Point(-l, y - l * 0.6));
                            }
                        }
                    }
                    Arm();
                }
            }
        }
        return rtb;
    }
}
