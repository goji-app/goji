using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace GodjiVpn.Controls;

/// <summary>
/// Огромный код страны макета 9a: Golos Bold, трекинг −0.07em. Выключено — только контур (Stroke,
/// без заливки), подключение/в сети — заливка. Кегль подгоняется под доступную ширину, чтобы буквы
/// (в том числе «LTE») никогда не обрезались; MaxFontSize — потолок макета (262 / десктоп 170).
/// </summary>
public sealed class CountryCodeText : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(CountryCodeText),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(CountryCodeText), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(CountryCodeText), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MaxFontSizeProperty = DependencyProperty.Register(
        nameof(MaxFontSize), typeof(double), typeof(CountryCodeText),
        new FrameworkPropertyMetadata(262.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public Brush? Fill { get => (Brush?)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public Brush? Stroke { get => (Brush?)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public static readonly DependencyProperty DecorProperty = DependencyProperty.Register(
        nameof(Decor), typeof(bool), typeof(CountryCodeText),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    /// Зимой: шапка Деда Мороза на первой букве и снег на кромках букв.
    public bool Decor { get => (bool)GetValue(DecorProperty); set => SetValue(DecorProperty, value); }
    public static readonly DependencyProperty CenteredProperty = DependencyProperty.Register(
        nameof(Centered), typeof(bool), typeof(CountryCodeText), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public bool Centered { get => (bool)GetValue(CenteredProperty); set => SetValue(CenteredProperty, value); }

    public double MaxFontSize { get => (double)GetValue(MaxFontSizeProperty); set => SetValue(MaxFontSizeProperty, value); }

    private static readonly Typeface Face = new(
        (FontFamily)Application.Current.FindResource("ManropeFamily"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    private FormattedText Format(double size) => new(
        Text ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Face, size, Brushes.Black,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private double FitSize(double availableWidth)
    {
        if (string.IsNullOrEmpty(Text)) return MaxFontSize;
        var size = MaxFontSize;
        var w = Format(size).WidthIncludingTrailingWhitespace - 0.07 * size * Text.Length; // трекинг −0.07em
        if (w > availableWidth && w > 0) size = Math.Max(24, size * availableWidth / w);
        return size;
    }

    private bool Winter => Decor && WinterOverlay.IsActive();

    public CountryCodeText()
    {
        void Refresh() { InvalidateMeasure(); InvalidateVisual(); }
        Loaded += (_, _) => WinterOverlay.DecorChanged += Refresh;
        Unloaded += (_, _) => WinterOverlay.DecorChanged -= Refresh;
    }
    private static double HatWidthFor(double size) => size * 0.30;

    /// Верхняя кромка заглавных, если рисовать от (0, -0.14·size): считается по настоящему контуру.
    private double CapTop(double size) => Format(size).BuildGeometry(new Point(0, -size * 0.14)).Bounds.Top;

    /// Запас сверху под шапку, если над буквами не хватает собственной пустоты шрифта.
    private double Inset(double size)
    {
        if (!Winter || string.IsNullOrEmpty(Text)) return 0;
        return Math.Max(0, HatWidthFor(size) * 104.0 / 120.0 * 0.82 + size * 0.02 - CapTop(size));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var avail = double.IsInfinity(availableSize.Width) ? 440 : availableSize.Width;
        var size = FitSize(avail);
        return new Size(avail, size * 0.89 + Inset(size));
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(Text)) return;
        var size = FitSize(ActualWidth);
        var ft = Format(size);
        var inset = Inset(size);
        var geo = ft.BuildGeometry(new Point(0, -size * 0.14 + inset));
        // Трекинг: сжимаем межбуквенный промежуток масштабом по X.
        var tracking = 0.07 * size * Text.Length;
        var scale = Math.Max(0.5, 1 - tracking / Math.Max(1, ft.WidthIncludingTrailingWhitespace));
        var shift = Centered ? Math.Max(0, (ActualWidth - ft.WidthIncludingTrailingWhitespace * scale) / 2) : 0;
        geo.Transform = new TransformGroup { Children = { new ScaleTransform(scale, 1), new TranslateTransform(shift, 0) } };
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
        dc.DrawGeometry(Fill, Stroke is null ? null : new Pen(Stroke, 2), geo);
        if (Winter)
        {
            var rim = SnowRim.Build(ft.BuildGeometry(new Point(0, -size * 0.14 + inset)), size);
            if (rim != null)
            {
                rim.Transform = geo.Transform;
                var brush = SnowRim.Brush(WinterOverlay.IsLightTheme());
                dc.DrawGeometry(brush, SnowRim.SoftEdge(brush, size), rim);
            }
        }
        dc.Pop();
        if (Winter)
        {
            // Шапка садится на верхнюю кромку первой буквы (границы — после трекинга и сдвига).
            var b = geo.Bounds;
            double hw = HatWidthFor(size), hh = hw * 104.0 / 120.0;
            SantaHat.Paint(dc, new Point(b.Left - hw * 0.10, b.Top - hh * 0.82), hw, -7);
        }
    }
}
