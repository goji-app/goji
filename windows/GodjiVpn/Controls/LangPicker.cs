using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GodjiVpn.Services;

namespace GodjiVpn.Controls;

/// <summary>Выбор языка: РУССКИЙ · ENGLISH · 中文 (активный — жирный, цвет Ink).</summary>
public sealed class LangPicker : StackPanel
{
    public LangPicker()
    {
        Orientation = Orientation.Horizontal;
        for (var i = 0; i < Loc.Codes.Length; i++)
        {
            var code = Loc.Codes[i];
            var tb = new TextBlock { Text = Loc.Titles[i], FontSize = 12, Margin = new Thickness(0, 6, 16, 6), Cursor = Cursors.Hand };
            tb.SetResourceReference(TextBlock.FontFamilyProperty, "JetBrainsMonoFamily");
            tb.MouseLeftButtonUp += (_, _) => Loc.Instance.Set(code);
            Children.Add(tb);
        }
        Loaded += (_, _) => { Loc.Instance.Changed += Paint; Paint(); };
        Unloaded += (_, _) => Loc.Instance.Changed -= Paint;
    }

    private void Paint()
    {
        for (var i = 0; i < Children.Count; i++)
        {
            var on = Loc.Codes[i] == Loc.Instance.Lang;
            var tb = (TextBlock)Children[i];
            tb.FontWeight = on ? FontWeights.Bold : FontWeights.Normal;
            tb.SetResourceReference(TextBlock.ForegroundProperty, on ? "InkBrush" : "TextSecondaryBrush");
        }
    }
}
