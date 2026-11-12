using System.ComponentModel;
using System.IO;
using System.Windows;

namespace GodjiVpn.Services;

/// <summary>Язык интерфейса (ru/en/zh). Ключ перевода — русская строка; нет перевода — показываем её же.
/// Таблица Assets/Loc.tsv: «ru ⇥ en ⇥ zh». Выбор сохраняется в lang.txt.</summary>
public sealed class Loc
{
    public static readonly string[] Codes = { "ru", "en", "zh" };
    public static readonly string[] Titles = { "РУССКИЙ", "ENGLISH", "中文" };
    private static readonly string Path_ = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GodjiVpn", "lang.txt");
    public static readonly Loc Instance = new();

    private readonly Dictionary<string, (string en, string zh)> _table = new();
    private readonly Dictionary<string, LocItem> _items = new();

    public string Lang { get; private set; } = "ru";
    public event Action? Changed;

    private Loc()
    {
        try
        {
            var info = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Loc.tsv"));
            using var r = new StreamReader(info.Stream);
            while (r.ReadLine() is { } line)
            {
                var p = line.Split('\t');
                if (p.Length >= 3) _table[p[0]] = (p[1], p[2]);
            }
        }
        catch { /* без таблицы остаётся русский */ }
        try
        {
            Lang = File.Exists(Path_) ? File.ReadAllText(Path_).Trim()
                : System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName is "ru" or "uk" or "be" ? "ru"
                : System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "zh" : "en";
        }
        catch { }
        if (Array.IndexOf(Codes, Lang) < 0) Lang = "ru";
    }

    public static string T(string ru) => Instance.Translate(ru);

    public string Translate(string ru) => Lang == "ru" || !_table.TryGetValue(ru, out var t) ? ru : Lang == "en" ? t.en : t.zh;

    public void Set(string lang)
    {
        if (lang == Lang || Array.IndexOf(Codes, lang) < 0) return;
        Lang = lang;
        try { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path_)!); File.WriteAllText(Path_, lang); } catch { }
        foreach (var i in _items.Values) i.Refresh();
        Changed?.Invoke();
    }

    internal LocItem Item(string ru)
    {
        if (!_items.TryGetValue(ru, out var i)) _items[ru] = i = new LocItem(this, ru);
        return i;
    }
}

/// <summary>Привязываемое значение одной строки: обновляется при смене языка.</summary>
public sealed class LocItem : INotifyPropertyChanged
{
    private readonly Loc _loc; private readonly string _ru;
    internal LocItem(Loc loc, string ru) { _loc = loc; _ru = ru; }
    public string Value => _loc.Translate(_ru);
    public event PropertyChangedEventHandler? PropertyChanged;
    internal void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
}
