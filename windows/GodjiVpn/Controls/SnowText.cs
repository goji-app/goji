using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace GodjiVpn.Controls
{
    /// Текст со снегом на буквах и, по желанию, шапкой Деда Мороза на первой букве.
    /// Вне зимнего сезона рисует обычный текст (как TextBlock с переносом по словам).
    public sealed class SnowText : FrameworkElement
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(SnowText),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty HasHatProperty = DependencyProperty.Register(
            nameof(HasHat), typeof(bool), typeof(SnowText),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(typeof(SnowText),
            new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(typeof(SnowText),
            new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(typeof(SnowText),
            new FrameworkPropertyMetadata(FontWeights.Normal, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(SnowText),
            new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

        public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public bool HasHat { get => (bool)GetValue(HasHatProperty); set => SetValue(HasHatProperty, value); }
        public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
        public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
        public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
        public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

        public SnowText()
        {
            IsHitTestVisible = false;
            void Refresh() { InvalidateMeasure(); InvalidateVisual(); }
            Loaded += (_, _) => WinterOverlay.DecorChanged += Refresh;
            Unloaded += (_, _) => WinterOverlay.DecorChanged -= Refresh;
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

        private FormattedText Format(string text, double maxWidth) => new FormattedText(
            text ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal), FontSize,
            Foreground ?? Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        { MaxTextWidth = Math.Max(1, maxWidth), Trimming = TextTrimming.None };

        private bool Hat => HasHat && WinterOverlay.IsActive() && !string.IsNullOrEmpty(Text);
        private double HatWidth => Math.Max(34, FontSize * 1.55);

        /// Место над текстом под шапку: шапка садится на верхнюю кромку первой буквы.
        private double TopPad(out Rect firstLetter)
        {
            firstLetter = Rect.Empty;
            if (!Hat) return 0;
            var g = Format(Text.Substring(0, 1), 1e6).BuildGeometry(new Point(0, 0));
            if (g == null || g.IsEmpty()) return 0;
            firstLetter = g.Bounds;
            return Math.Max(0, HatWidth * 104.0 / 120.0 * 0.82 - firstLetter.Top);
        }

        protected override Size MeasureOverride(Size available)
        {
            var w = double.IsInfinity(available.Width) ? 1e6 : available.Width;
            var ft = Format(Text, w);
            return new Size(Math.Min(w, Math.Ceiling(ft.Width)), Math.Ceiling(ft.Height + TopPad(out _)));
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (string.IsNullOrEmpty(Text)) return;
            double pad = TopPad(out var first);
            var ft = Format(Text, ActualWidth);
            dc.DrawText(ft, new Point(0, pad));
            if (!WinterOverlay.IsActive()) return;

            var rim = SnowRim.Build(ft.BuildGeometry(new Point(0, pad)), FontSize);
            if (rim != null)
            {
                var brush = SnowRim.Brush(WinterOverlay.IsLightTheme());
                dc.DrawGeometry(brush, SnowRim.SoftEdge(brush, FontSize), rim);
            }
            if (Hat && !first.IsEmpty)
            {
                double w = HatWidth, h = w * 104.0 / 120.0;
                SantaHat.Paint(dc, new Point(first.Left - w * 0.12, pad + first.Top - h * 0.82), w, -7);
            }
        }
    }
}
