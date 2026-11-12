using System.Windows;
using System.Windows.Media;

namespace GodjiVpn.Controls;

/// <summary>Шкала пинга макета 9a: три равных сегмента (&lt;500 · &lt;1000 · ≥1000 мс) с зазором и метка
/// цвета Ink. Цвет не единственный носитель смысла — число пинга всегда рядом.</summary>
public sealed class PingBar : FrameworkElement
{
    public static readonly DependencyProperty PingMsProperty = DependencyProperty.Register(
        nameof(PingMs), typeof(int?), typeof(PingBar), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MarkerBrushProperty = DependencyProperty.Register(
        nameof(MarkerBrush), typeof(Brush), typeof(PingBar), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty BarHeightProperty = DependencyProperty.Register(
        nameof(BarHeight), typeof(double), typeof(PingBar), new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public int? PingMs { get => (int?)GetValue(PingMsProperty); set => SetValue(PingMsProperty, value); }
    public Brush MarkerBrush { get => (Brush)GetValue(MarkerBrushProperty); set => SetValue(MarkerBrushProperty, value); }
    public double BarHeight { get => (double)GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }

    private static readonly Brush Good = Frozen("#6E8B79"), Ok = Frozen("#B39A62"), Bad = Frozen("#A5655E");
    private static Brush Frozen(string hex) { var b = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!; b.Freeze(); return b; }

    protected override Size MeasureOverride(Size a) => new(double.IsInfinity(a.Width) ? 96 : a.Width, BarHeight + 8);

    protected override void OnRender(DrawingContext dc)
    {
        var gap = 2.0; var h = BarHeight; var y = (ActualHeight - h) / 2;
        var seg = (ActualWidth - 2 * gap) / 3;
        dc.DrawRectangle(Good, null, new Rect(0, y, seg, h));
        dc.DrawRectangle(Ok, null, new Rect(seg + gap, y, seg, h));
        dc.DrawRectangle(Bad, null, new Rect(2 * (seg + gap), y, seg, h));
        if (PingMs is int ms and >= 0)
        {
            var x = Math.Min(0.97, ms / 1000.0 * 0.97) * ActualWidth;
            dc.DrawRectangle(MarkerBrush, null, new Rect(x, 0, 3, ActualHeight));
        }
    }
}
