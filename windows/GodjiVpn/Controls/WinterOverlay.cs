using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace GodjiVpn.Controls
{
    /// Новогодняя тема поверх оболочки: снег и качающиеся гирлянды.
    /// Активна с 1 ноября по 31 января; вне сезона элемент ничего не рисует и не тикает.
    /// Провод качается как гибкая верёвка: контрольная точка провиса плывёт по трём гармоникам,
    /// лампочки едут вдоль провода и отстают от него, как маятники.
    public class WinterOverlay : FrameworkElement
    {
        /// Сезон: 1 декабря — 31 января.
        public static bool InSeason()
        {
            var m = DateTime.Now.Month;
            return m == 12 || m == 1;
        }

        /// Переключатель «Новогоднее оформление» из настроек.
        public static bool Enabled { get; private set; } = true;

        /// Срабатывает при включении/выключении оформления: элементы перерисовываются сразу, без перезапуска.
        public static event Action? DecorChanged;

        public static void SetEnabled(bool on)
        {
            if (Enabled == on) return;
            Enabled = on;
            DecorChanged?.Invoke();
        }

        /// Рисовать ли оформление сейчас (сезон и не выключено в настройках).
        public static bool IsActive() => Enabled && InSeason();

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
            public Bulb[] Bulbs = Array.Empty<Bulb>();
        }

        private static readonly Color[] BulbColors =
        {
            Color.FromRgb(0xFF,0x5A,0x4E), Color.FromRgb(0xFF,0xD2,0x57), Color.FromRgb(0x63,0xD6,0x8C),
            Color.FromRgb(0x6A,0xA9,0xFF), Color.FromRgb(0xFF,0x9D,0x5C)
        };
        private static readonly Pen WirePen = MakePen(Color.FromArgb(255, 0x2C, 0x36, 0x34), 1.6);
        private static readonly Pen SocketLinePen = MakePen(Color.FromArgb(255, 0x20, 0x29, 0x27), 0.8);
        private static readonly Brush SocketBrush = MakeBrush(Color.FromRgb(0x3A, 0x45, 0x42));
        private static readonly Brush GlassCore = MakeBrush(Color.FromArgb(77, 0xFF, 0xF8, 0xD9));
        private static readonly Brush Filament = MakeBrush(Color.FromArgb(217, 0xFF, 0xF8, 0xD9));
        private static readonly Brush GlassHi = MakeBrush(Color.FromArgb(128, 0xFF, 0xFF, 0xFF));
        private static readonly Geometry GlassGeom = Freeze("M0,10 C4.8,10 7.8,13 7.8,17 C7.8,22.2 4.4,25.2 0,25.2 C-4.4,25.2 -7.8,22.2 -7.8,17 C-7.8,13 -4.8,10 0,10 Z");
        private static readonly Pen FilamentPen = MakeFilamentPen();
        private static Pen MakeFilamentPen() { var p = new Pen(Filament, 0.9); p.Freeze(); return p; }
        private static readonly Pen GlassStroke = MakePen(Color.FromArgb(77, 0, 0, 0), 0.7);
        private static readonly RadialGradientBrush[] GlowBrushes = MakeGlows();

        private readonly Random _rnd = new Random();
        private readonly List<Flake> _flakes = new List<Flake>();
        private readonly List<Strand> _strands = new List<Strand>();
        private ImageSource[] _backSprites, _frontSprites;
        private Color _spriteColor;
        private double _t, _lastSec, _acc;

        private const double Tau = Math.PI * 2;

        private static Pen MakePen(Color c, double w)
        {
            var b = new SolidColorBrush(c); b.Freeze();
            var p = new Pen(b, w); p.Freeze();
            return p;
        }
        private static Brush MakeBrush(Color c)
        {
            var b = new SolidColorBrush(c); b.Freeze();
            return b;
        }
        private static Geometry Freeze(string data)
        {
            var g = Geometry.Parse(data);
            g.Freeze();
            return g;
        }
        private static RadialGradientBrush[] MakeGlows()
        {
            var list = new List<RadialGradientBrush>();
            foreach (var c in BulbColors)
            {
                var b = new RadialGradientBrush
                {
                    GradientStops =
                    {
                        new GradientStop(WithAlpha(c, 217), 0),
                        new GradientStop(WithAlpha(c, 82), 0.45),
                        new GradientStop(WithAlpha(c, 0), 1)
                    }
                };
                b.Freeze();
                list.Add(b);
            }
            return list.ToArray();
        }

        private static Color WithAlpha(Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);

        private double R(double a, double b) => a + _rnd.NextDouble() * (b - a);

        public WinterOverlay()
        {
            IsHitTestVisible = false;
            Loaded += (s, e) => { DecorChanged += Sync; Sync(); };
            Unloaded += (s, e) => { DecorChanged -= Sync; CompositionTarget.Rendering -= OnRendering; };
        }

        /// Включает или выключает покадровую отрисовку по состоянию оформления.
        private void Sync()
        {
            CompositionTarget.Rendering -= OnRendering;
            if (IsActive())
            {
                if (_flakes.Count == 0) BuildScene();
                CompositionTarget.Rendering += OnRendering;
            }
            InvalidateVisual();
        }

        private void OnRendering(object sender, EventArgs e)
        {
            var sec = ((RenderingEventArgs)e).RenderingTime.TotalSeconds;
            double dt = _lastSec == 0 ? 0.016 : sec - _lastSec;
            _lastSec = sec;
            if (dt <= 0 || dt > 0.1) dt = 0.016;
            if (!SystemParameters.ClientAreaAnimation) dt = 0;
            // ~30 к/с вместо 60: снегу хватает, а процессор и батарея ноутбука целее.
            _acc += dt;
            if (_acc < 1.0 / 30) return;
            dt = Math.Min(_acc, 0.1); _acc = 0;
            _t += dt;
            UpdateGarlands(dt, _t);
            UpdateSnow(dt);
            InvalidateVisual();
        }

        private void BuildScene()
        {
            double w = Math.Max(ActualWidth, 300);
            _flakes.Clear();
            for (int i = 0; i < 64; i++) _flakes.Add(MakeFlake(w, Math.Max(ActualHeight, 300), false));
            for (int i = 0; i < 8; i++) _flakes.Add(MakeFlake(w, Math.Max(ActualHeight, 300), true));

            _strands.Clear();
            _strands.Add(MakeStrand(w, -4, w / 2.0, w + 4, 10, 178, 16, 10, 1.0));
            _strands.Add(MakeStrand(w, -4, w / 2.0, w + 4, 54, 118, 46, 9, 0.92));
        }

        private Strand MakeStrand(double w, double x0, double cx, double x2, double y0, double cy, double y2, int n, double sc)
        {
            var bulbs = new Bulb[n];
            for (int k = 0; k < n; k++)
            {
                bulbs[k] = new Bulb
                {
                    T = 0.06 + 0.88 * k / (n - 1),
                    Px = 0, Init = false, Tilt = 0,
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

        private static double Wind(double t)
        {
            return 16 * Math.Sin(t * 0.11 + 1.3) + 9 * Math.Sin(t * 0.31 + 4) + 22 * Math.Sin(t * 0.043) * Math.Sin(t * 0.097);
        }

        private double StrandSway(Strand d, double t)
        {
            return d.A * (0.62 * Math.Sin(Tau / 6.4 * t + d.P1)
                        + 0.26 * Math.Sin(Tau / 2.7 * t + d.P2)
                        + 0.12 * Math.Sin(Tau / 11.5 * t + d.P3));
        }

        private static double Qp(double t, double a, double c, double b)
        {
            double u = 1 - t;
            return u * u * a + 2 * u * t * c + t * t * b;
        }

        private void UpdateGarlands(double dt, double t)
        {
            for (int gi = 0; gi < _strands.Count; gi++)
            {
                var d = _strands[gi];
                double dx = StrandSway(d, t) * 2.2;
                double dy = 3 * Math.Sin(Tau / 8.3 * t + d.P2);
                foreach (var bl in d.Bulbs)
                {
                    double x = Qp(bl.T, d.X0, d.Cx + dx, d.X2);
                    double y = Qp(bl.T, d.Y0, d.Cy + dy, d.Y2);
                    double vel = 0;
                    if (bl.Init && dt > 0) vel = (x - bl.Px) / dt;
                    bl.Px = x; bl.Init = true;
                    double target = Math.Max(-11, Math.Min(11, -vel * 0.38)) + 1.1 * Math.Sin(t * bl.Wf * Tau + bl.Pp);
                    bl.Tilt += (target - bl.Tilt) * Math.Min(1, dt * bl.Resp * 3);
                }
            }
        }

        private void UpdateSnow(double dt)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 50) return;
            for (int i = 0; i < _flakes.Count; i++)
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

        protected override void OnRender(DrawingContext dc)
        {
            if (!IsActive() || _flakes.Count == 0) return;
            double w = ActualWidth, h = ActualHeight;
            if (w < 50 || h < 50) return;
            if (_strands.Count == 0) BuildScene();

            var snow = SnowColor();
            EnsureSprites(snow);

            // Гирлянды: гнущийся провод и лампочки-маятники.
            foreach (var d in _strands)
            {
                double dx = StrandSway(d, _t) * 2.2;
                double dy = 3 * Math.Sin(Tau / 8.3 * _t + d.P2);

                var wire = new StreamGeometry();
                using (var gctx = wire.Open())
                {
                    gctx.BeginFigure(new Point(d.X0, d.Y0), false, false);
                    gctx.QuadraticBezierTo(new Point(d.Cx + dx, d.Cy + dy), new Point(d.X2, d.Y2), true, true);
                }
                dc.DrawGeometry(null, WirePen, wire);

                foreach (var bl in d.Bulbs)
                {
                    double x = Qp(bl.T, d.X0, d.Cx + dx, d.X2);
                    double y = Qp(bl.T, d.Y0, d.Cy + dy, d.Y2);
                    double sc = d.Sc;
                    var m = new Matrix();
                    m.Scale(sc, sc);
                    m.Translate(x, y);
                    m.RotateAt(bl.Tilt, x, y);
                    dc.PushTransform(new MatrixTransform(m));
                    dc.DrawEllipse(GlowBrushes[bl.ColorIdx], null, new Point(0, 17), 21, 21);
                    dc.DrawRectangle(SocketBrush, null, new Rect(-3.6, 3, 7.2, 7.4));
                    dc.DrawLine(SocketLinePen, new Point(-3.6, 4.8), new Point(3.6, 4.8));
                    dc.DrawLine(SocketLinePen, new Point(-3.6, 6.6), new Point(3.6, 6.6));
                    dc.DrawLine(MakeBulbStem(), new Point(0, 0), new Point(0, 3.4));
                    dc.DrawGeometry(GlassFill(bl.ColorIdx), GlassStroke, GlassGeom);
                    dc.DrawEllipse(GlassCore, null, new Point(0, 17.4), 3.4, 5);
                    dc.DrawLine(FilamentPen, new Point(-1.8, 14.5), new Point(0, 19));
                    dc.DrawLine(FilamentPen, new Point(0, 19), new Point(1.8, 14.5));
                    dc.DrawEllipse(GlassHi, null, new Point(-3, 13.6), 1.7, 2.6);
                    dc.Pop();
                }
            }

            // Снег: сзади мелкий и блеклый, спереди крупный и мягкий — поверх всего контента.
            foreach (var f in _flakes)
            {
                double rx = f.X + Math.Sin(f.Ph * f.SwayF * Tau) * f.SwayA + Math.Sin(f.Ph2 * 0.6 + 1.3) * f.A2;
                double sz = f.Size; // размер квадрата спрайта в DIP
                var img = f.Front ? _frontSprites[f.Spr] : _backSprites[f.Spr];
                var m = new Matrix();
                m.Translate(rx - sz / 2, f.Y - sz / 2);
                m.RotateAt(f.Rot, rx, f.Y);
                dc.PushTransform(new MatrixTransform(m));
                dc.PushOpacity(f.Alpha);
                dc.DrawImage(img, new Rect(0, 0, sz, sz));
                dc.Pop();
                dc.Pop();
            }
        }

        private int _glassFillIdx = -1;
        private Brush GlassFill(int idx)
        {
            if (_glassFillIdx != idx)
            {
                var b = new SolidColorBrush(BulbColors[idx]); b.Freeze();
                _glassFill = b; _glassFillIdx = idx;
            }
            return _glassFill;
        }
        private Brush _glassFill;

        private Pen? _stemPen;
        private Pen MakeBulbStem() => _stemPen ??= MakePen(Color.FromArgb(255, 0x2C, 0x36, 0x34), 1.8);

        internal static bool IsLightTheme()
        {
            if (Application.Current?.TryFindResource("BgSolidBrush") is SolidColorBrush b && b.Color != default)
                return (0.299 * b.Color.R + 0.587 * b.Color.G + 0.114 * b.Color.B) / 255.0 > 0.5;
            return false;
        }

        private Color SnowColor() => IsLightTheme() ? Color.FromRgb(0x8F, 0xA8, 0xC4) : Colors.White;

        private void EnsureSprites(Color snow)
        {
            if (_backSprites != null && _spriteColor == snow) return;
            _spriteColor = snow;
            _backSprites = new ImageSource[3];
            _frontSprites = new ImageSource[3];
            for (int i = 0; i < 3; i++)
            {
                double[] backSize = { 7, 6, 5 };
                double[] frontSize = { 30, 26, 22 };
                _backSprites[i] = RenderFlake((int)backSize[i], i, snow, 0);
                _frontSprites[i] = RenderFlake((int)frontSize[i], i, snow, i == 0 ? 1.2 : i == 1 ? 1.0 : 0.9);
            }
        }

        /// Дендрит (0), гексагональная пластина (1), игольчатая звезда (2) — 6-лучевой контур из линий.
        private ImageSource RenderFlake(double px, int variant, Color color, double blur)
        {
            int pad = 8;
            int s = (int)Math.Ceiling(px) + pad;
            int ss = s * 2;
            var dv = new DrawingVisual();
            using (var g = dv.RenderOpen())
            {
                double r = px / 2;
                var pen = MakePen(Color.FromArgb(242, color.R, color.G, color.B), Math.Max(0.7, px * 0.05));
                for (int k = 0; k < 6; k++)
                {
                    double ang = k * 360.0 / 6.0;
                    g.PushTransform(new RotateTransform(ang));
                    if (variant == 0)
                    {
                        g.DrawLine(pen, new Point(0, 0), new Point(0, -r));
                        double[,] brs = { { 0.42, 0.42 }, { 0.6, 0.3 }, { 0.78, 0.18 } };
                        for (int i = 0; i < 3; i++)
                        {
                            double by = -r * brs[i, 0], bl = r * brs[i, 1];
                            g.DrawLine(pen, new Point(0, by), new Point(bl * 0.5, by - bl * 0.87));
                            g.DrawLine(pen, new Point(0, by), new Point(-bl * 0.5, by - bl * 0.87));
                        }
                        g.DrawLine(pen, new Point(0, -r * 0.9), new Point(r * 0.12, -r * 0.78));
                        g.DrawLine(pen, new Point(0, -r * 0.9), new Point(-r * 0.12, -r * 0.78));
                    }
                    else if (variant == 1)
                    {
                        double[] rf = { 1, 0.62, 0.3 };
                        foreach (var rfi in rf)
                        {
                            for (int i = 0; i < 6; i++)
                            {
                                double a1 = i * Tau / 6, a2 = (i + 1) * Tau / 6;
                                g.DrawLine(pen, new Point(Math.Cos(a1) * r * rfi, Math.Sin(a1) * r * rfi),
                                                new Point(Math.Cos(a2) * r * rfi, Math.Sin(a2) * r * rfi));
                            }
                        }
                        g.DrawLine(pen, new Point(0, 0), new Point(0, -r));
                    }
                    else
                    {
                        g.DrawLine(pen, new Point(0, 0), new Point(0, -r));
                        double[] pp = { 0.5, 0.72 };
                        foreach (var p in pp)
                        {
                            double y = -r * p, l = r * 0.34 * (p / 0.5) * 0.9;
                            g.DrawLine(pen, new Point(0, y), new Point(l, y - l * 0.6));
                            g.DrawLine(pen, new Point(0, y), new Point(-l, y - l * 0.6));
                        }
                    }
                    g.Pop();
                }
            }
            if (blur > 0) dv.Effect = new System.Windows.Media.Effects.BlurEffect { Radius = blur };
            var rtb = new RenderTargetBitmap(ss, ss, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }
    }
}
