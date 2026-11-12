using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using GodjiVpn.Services;

namespace GodjiVpn.Controls;

/// <summary>Выбор языка: РУССКИЙ · ENGLISH · 中文 (активный — жирный, цвет Ink).</summary>
public sealed class LangPicker : StackPanel
{
    public LangPicker()
    {
        Orientation = Avalonia.Layout.Orientation.Horizontal;
        for (var i = 0; i < Loc.Codes.Length; i++)
        {
            var code = Loc.Codes[i];
            var tb = new TextBlock { Text = Loc.Titles[i], FontSize = 12, Margin = new Thickness(0, 6, 16, 6), Cursor = new Cursor(StandardCursorType.Hand) };
            tb[!TextBlock.FontFamilyProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Mono");
            tb.PointerReleased += (_, _) => Loc.Instance.Set(code);
            Children.Add(tb);
        }
        AttachedToVisualTree += (_, _) => { Loc.Instance.Changed += Paint; Paint(); };
        DetachedFromVisualTree += (_, _) => Loc.Instance.Changed -= Paint;
    }

    private void Paint()
    {
        for (var i = 0; i < Children.Count; i++)
        {
            var on = Loc.Codes[i] == Loc.Instance.Lang;
            var tb = (TextBlock)Children[i];
            tb.FontWeight = on ? FontWeight.Bold : FontWeight.Normal;
            tb[!TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(on ? "InkBrush" : "TextSecondaryBrush");
        }
    }
}
