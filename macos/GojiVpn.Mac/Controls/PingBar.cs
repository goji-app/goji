using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace GodjiVpn.Controls;

/// <summary>Шкала пинга макета 9a: три равных сегмента (&lt;500 · &lt;1000 · ≥1000 мс) с зазором и метка цвета
/// Ink. Цвет не единственный носитель смысла — число пинга всегда рядом.</summary>
public sealed class PingBar : Control
{
    public static readonly StyledProperty<int?> PingMsProperty = AvaloniaProperty.Register<PingBar, int?>(nameof(PingMs));
    public static readonly StyledProperty<IBrush?> MarkerBrushProperty = AvaloniaProperty.Register<PingBar, IBrush?>(nameof(MarkerBrush), Brushes.Black);
    public static readonly StyledProperty<double> BarHeightProperty = AvaloniaProperty.Register<PingBar, double>(nameof(BarHeight), 6);

    public int? PingMs { get => GetValue(PingMsProperty); set => SetValue(PingMsProperty, value); }
    public IBrush? MarkerBrush { get => GetValue(MarkerBrushProperty); set => SetValue(MarkerBrushProperty, value); }
    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }

    static PingBar()
    {
        AffectsMeasure<PingBar>(BarHeightProperty);
        AffectsRender<PingBar>(PingMsProperty, MarkerBrushProperty, BarHeightProperty);
    }

    private static readonly IBrush Good = new SolidColorBrush(Color.Parse("#6E8B79"));
    private static readonly IBrush Ok = new SolidColorBrush(Color.Parse("#B39A62"));
    private static readonly IBrush Bad = new SolidColorBrush(Color.Parse("#A5655E"));

    protected override Size MeasureOverride(Size a) => new(double.IsInfinity(a.Width) ? 96 : a.Width, BarHeight + 8);

    public override void Render(DrawingContext dc)
    {
        const double gap = 2;
        var h = BarHeight; var y = (Bounds.Height - h) / 2;
        var seg = (Bounds.Width - 2 * gap) / 3;
        dc.DrawRectangle(Good, null, new Rect(0, y, seg, h));
        dc.DrawRectangle(Ok, null, new Rect(seg + gap, y, seg, h));
        dc.DrawRectangle(Bad, null, new Rect(2 * (seg + gap), y, seg, h));
        if (PingMs is int ms and >= 0)
        {
            var x = Math.Min(0.97, ms / 1000.0 * 0.97) * Bounds.Width;
            dc.DrawRectangle(MarkerBrush, null, new Rect(x, 0, 3, Bounds.Height));
        }
    }
}
