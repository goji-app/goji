using Avalonia.Data;
using Avalonia.Markup.Xaml;
using GodjiVpn.Services;

namespace GodjiVpn.Controls;

/// <summary>{loc:T 'Русский текст'} — строка интерфейса, переводится на лету при смене языка.</summary>
public sealed class TExtension : MarkupExtension
{
    public string Key { get; set; } = "";
    public TExtension() { }
    public TExtension(string key) { Key = key; }
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding(nameof(LocItem.Value)) { Source = Loc.Instance.Item(Key), Mode = BindingMode.OneWay };
}
