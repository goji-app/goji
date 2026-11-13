using System;
using System.Windows;
using System.Windows.Media;

namespace GodjiVpn.Controls
{
    /// Шапка Деда Мороза, сидящая на главной букве (код страны, «Войти»).
    /// Вне зимнего сезона элемент схлопывается в ноль и ничего не рисует.
    public class SantaHat : FrameworkElement
    {
        public static readonly DependencyProperty HatWidthProperty = DependencyProperty.Register(
            nameof(HatWidth), typeof(double), typeof(SantaHat), new PropertyMetadata(62.0, OnHatWidthChanged));

        public double HatWidth
        {
            get => (double)GetValue(HatWidthProperty);
            set => SetValue(HatWidthProperty, value);
        }

        private static void OnHatWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var hat = (SantaHat)d;
            double w = WinterOverlay.IsActive() ? (double)e.NewValue : 0;
            hat.Width = w;
            hat.Height = w * 104.0 / 120.0;
        }

        private static readonly Geometry Cone = Freeze("M14,68 C12,44 26,16 58,8 C76,4 90,10 96,20 C99,26 97,32 92,32 C90,44 88,56 88,68 Z");
        private static readonly Geometry Shade = Freeze("M92,32 C90,44 88,56 88,68 L78,68 C80,54 84,42 87,33 Z");
        private static readonly Geometry Highlight = Freeze("M18,62 C17,42 28,20 52,10 C36,22 25,42 25,62 Z");
        private static readonly Geometry Fur = Freeze("M8,62 C38,74 80,70 106,56 L110,71 C82,86 36,90 12,77 C4,74 3,67 8,62 Z");

        public SantaHat()
        {
            IsHitTestVisible = false;
            double w = WinterOverlay.IsActive() ? HatWidth : 0;
            Width = w;
            Height = w * 104.0 / 120.0;
        }

        private static Geometry Freeze(string data)
        {
            var g = Geometry.Parse(data);
            g.Freeze();
            return g;
        }

        protected override void OnRender(DrawingContext dc) => Paint(dc, new Point(0, 0), ActualWidth, 0);

        /// Рисует шапку с левым верхним углом в [at], шириной [width], наклоном [angle]° вокруг центра.
        /// Используется и самим элементом, и текстовыми контролами, у которых шапка садится на букву.
        internal static void Paint(DrawingContext dc, Point at, double width, double angle)
        {
            double scale = width / 120.0;
            if (scale <= 0 || double.IsNaN(scale)) return;
            var mx = new Matrix();
            mx.Scale(scale, scale);
            mx.RotateAt(angle, width / 2, width * 104.0 / 120.0 / 2);
            mx.Translate(at.X, at.Y);
            dc.PushTransform(new MatrixTransform(mx));

            var red = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(120, 104),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0xE8,0x50,0x5B), 0),
                    new GradientStop(Color.FromRgb(0xC9,0x30,0x3C), 0.55),
                    new GradientStop(Color.FromRgb(0x9E,0x1F,0x2E), 1)
                }
            };
            red.Freeze();
            var fur = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 104),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0xFB,0xFD,0xFF), 0),
                    new GradientStop(Color.FromRgb(0xDC,0xE3,0xEA), 1)
                }
            };
            fur.Freeze();
            var shade = new SolidColorBrush(Color.FromArgb(128, 0x8F, 0x18, 0x26)); shade.Freeze();
            var hi = new SolidColorBrush(Color.FromArgb(97, 0xF2, 0x71, 0x7A)); hi.Freeze();
            var furLine = new SolidColorBrush(Color.FromArgb(191, 0xC7, 0xD0, 0xDA)); furLine.Freeze();
            var pomArc = new SolidColorBrush(Color.FromArgb(166, 0xC9, 0xD2, 0xDB)); pomArc.Freeze();

            dc.DrawGeometry(red, null, Cone);
            dc.DrawGeometry(shade, null, Shade);
            dc.DrawGeometry(hi, null, Highlight);
            dc.DrawEllipse(fur, null, new Point(96, 19), 10.5, 10.5);
            dc.DrawLine(new Pen(pomArc, 1.6), new Point(89, 25), new Point(105, 23));
            dc.DrawGeometry(fur, null, Fur);
            dc.DrawLine(new Pen(furLine, 1.4), new Point(10, 64), new Point(104, 58));

            dc.Pop();
        }
    }
}
