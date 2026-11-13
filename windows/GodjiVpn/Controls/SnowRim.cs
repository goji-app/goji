using System;
using System.Windows;
using System.Windows.Media;

namespace GodjiVpn.Controls
{
    /// Сугробы на буквах по настоящему контуру шрифта: копия букв, сдвинутая вверх, минус копия,
    /// сдвинутая вниз. Остаётся полоса вдоль каждой верхней кромки — на плоских верхушках, дугах «О»
    /// и на дне внутренних просветов; на вертикалях ничего не остаётся, как и у настоящего снега.
    internal static class SnowRim
    {
        public static Geometry? Build(Geometry glyphs, double em)
        {
            if (glyphs == null || glyphs.IsEmpty()) return null;
            try
            {
                double lift = Math.Max(1.2, em * 0.025), depth = Math.Max(2.2, em * 0.055);
                Geometry Shift(double dx, double dy)
                {
                    var g = glyphs.Clone();
                    g.Transform = new TranslateTransform(dx, dy);
                    return g;
                }
                // Снег держится только на почти горизонтальных кромках: копии, сдвинутые вниз-влево и
                // вниз-вправо, съедают его на наклонных штрихах (V, N, А), оставляя шапки на верхушках.
                Geometry rim = Shift(0, -lift);
                foreach (var (dx, dy) in new[] { (0.0, depth), (-depth, depth), (depth, depth) })
                    rim = Geometry.Combine(rim, Shift(dx, dy), GeometryCombineMode.Exclude, null);
                return rim;
            }
            catch (System.InvalidOperationException) { return null; }
        }

        public static Brush Brush(bool light)
        {
            var b = new SolidColorBrush(light ? Color.FromRgb(0x8F, 0xA8, 0xC4) : Colors.White);
            b.Freeze();
            return b;
        }

        /// Скруглённые углы сугроба: тонкий обвод тем же цветом.
        public static Pen SoftEdge(Brush b, double em)
        {
            var p = new Pen(b, System.Math.Max(0.8, em * 0.012)) { LineJoin = PenLineJoin.Round };
            p.Freeze();
            return p;
        }
    }
}
