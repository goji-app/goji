using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace GodjiVpn.Controls;

/// Шапка Деда Мороза, сидящая на главной букве (код страны, «Войти»).
/// Вне зимнего сезона элемент схлопывается в ноль и ничего не рисует.
public sealed class SantaHat : Control
{
    private static readonly Geometry Cone = Parse("M14,68 C12,44 26,16 58,8 C76,4 90,10 96,20 C99,26 97,32 92,32 C90,44 88,56 88,68 Z");
    private static readonly Geometry Shade = Parse("M92,32 C90,44 88,56 88,68 L78,68 C80,54 84,42 87,33 Z");
    private static readonly Geometry Highlight = Parse("M18,62 C17,42 28,20 52,10 C36,22 25,42 25,62 Z");
    private static readonly Geometry Fur = Parse("M8,62 C38,74 80,70 106,56 L110,71 C82,86 36,90 12,77 C4,74 3,67 8,62 Z");
    private static readonly Geometry PomArc = Parse("M89,25 A10.5 10.5 0 0 0 105,23");
    private static readonly Geometry FurLine = Parse("M10,64 C40,75 80,71 104,58");

    private static Geometry Parse(string d) => StreamGeometry.Parse(d);

    public SantaHat()
    {
        IsHitTestVisible = false;
        Apply();
        AttachedToVisualTree += (_, _) => WinterOverlay.DecorChanged += Apply;
        DetachedFromVisualTree += (_, _) => WinterOverlay.DecorChanged -= Apply;
    }

    private void Apply()
    {
        var w = WinterOverlay.IsActive() ? 62.0 : 0;
        Width = w;
        Height = w * 104.0 / 120.0;
        InvalidateVisual();
    }

    public override void Render(DrawingContext dc)
    {
        var scale = Width / 120.0;
        if (scale <= 0 || double.IsNaN(scale)) return;
        var red = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(new Point(0, 0), RelativeUnit.Relative),
            EndPoint = new RelativePoint(new Point(1, 1), RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#E8505B"), 0),
                new GradientStop(Color.Parse("#C9303C"), 0.55),
                new GradientStop(Color.Parse("#9E1F2E"), 1)
            }
        };
        var fur = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(new Point(0, 0), RelativeUnit.Relative),
            EndPoint = new RelativePoint(new Point(0, 1), RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#FBFDFF"), 0),
                new GradientStop(Color.Parse("#DCE3EA"), 1)
            }
        };
        var shade = new SolidColorBrush(Color.Parse("#808F1826"));
        var hi = new SolidColorBrush(Color.Parse("#61F2717A"));
        var furLine = new Pen(new SolidColorBrush(Color.Parse("#BFC7D0DA")), 1.4);
        var pomArc = new Pen(new SolidColorBrush(Color.Parse("#A6C9D2DB")), 1.6);

        using (dc.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            dc.DrawGeometry(red, null, Cone);
            dc.DrawGeometry(shade, null, Shade);
            dc.DrawGeometry(hi, null, Highlight);
            dc.DrawEllipse(fur, null, new Point(96, 19), 10.5, 10.5);
            dc.DrawGeometry(null, pomArc, PomArc);
            dc.DrawGeometry(fur, null, Fur);
            dc.DrawGeometry(null, furLine, FurLine);
        }
    }
}
