using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace GodjiVpn.Controls;

/// <summary>
/// Огромный код страны макета 9a: Golos Bold, трекинг −0.07em. Выключено — только контур (Stroke, без
/// заливки), подключение/в сети — заливка. Кегль подгоняется под доступную ширину, чтобы буквы
/// (в том числе «LTE») не обрезались; MaxFontSize — потолок макета (десктоп 170).
/// </summary>
public sealed class CountryCodeText : Control
{
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<CountryCodeText, string>(nameof(Text), "");
    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<CountryCodeText, IBrush?>(nameof(Fill));
    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<CountryCodeText, IBrush?>(nameof(Stroke));
    public static readonly StyledProperty<double> MaxFontSizeProperty = AvaloniaProperty.Register<CountryCodeText, double>(nameof(MaxFontSize), 170);

    public static readonly StyledProperty<bool> CenteredProperty = AvaloniaProperty.Register<CountryCodeText, bool>(nameof(Centered));
    public bool Centered { get => GetValue(CenteredProperty); set => SetValue(CenteredProperty, value); }

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double MaxFontSize { get => GetValue(MaxFontSizeProperty); set => SetValue(MaxFontSizeProperty, value); }

    static CountryCodeText()
    {
        AffectsMeasure<CountryCodeText>(TextProperty, MaxFontSizeProperty);
        AffectsRender<CountryCodeText>(TextProperty, FillProperty, StrokeProperty, MaxFontSizeProperty, CenteredProperty);
    }

    private static readonly Typeface Face = new("avares://GojiVpn/Assets/Fonts#Golos Text", FontStyle.Normal, FontWeight.Bold);

    private FormattedText Format(double size) => new(Text ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Face, size, Brushes.Black);

    private double FitSize(double availableWidth)
    {
        if (string.IsNullOrEmpty(Text) || double.IsInfinity(availableWidth) || availableWidth <= 0) return MaxFontSize;
        var size = MaxFontSize;
        var w = Format(size).Width - 0.07 * size * Text.Length; // трекинг −0.07em
        if (w > availableWidth && w > 0) size = Math.Max(24, size * availableWidth / w);
        return size;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var avail = double.IsInfinity(availableSize.Width) ? 440 : availableSize.Width;
        return new Size(avail, FitSize(avail) * 0.89);
    }

    public override void Render(DrawingContext context)
    {
        if (string.IsNullOrEmpty(Text)) return;
        var size = FitSize(Bounds.Width);
        var ft = Format(size);
        var geo = ft.BuildGeometry(new Point(0, -size * 0.14));
        if (geo == null) return;
        var tracking = 0.07 * size * Text.Length;
        var scale = Math.Max(0.5, 1 - tracking / Math.Max(1, ft.Width));
        var shift = Centered ? Math.Max(0, (Bounds.Width - ft.Width * scale) / 2) : 0;
        using (context.PushClip(new Rect(Bounds.Size)))
        using (context.PushTransform(Matrix.CreateScale(scale, 1) * Matrix.CreateTranslation(shift, 0)))
            context.DrawGeometry(Fill, Stroke is null ? null : new Pen(Stroke, 2), geo);
    }
}
