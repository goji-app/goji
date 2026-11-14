using System;
using System.Globalization;
using System.IO;

namespace GodjiSetup;

/// <summary>Строки интерфейса (ru/en/zh) — тройки inline, как таблица Loc.tsv приложения.
/// Язык: %LOCALAPPDATA%\GodjiVpn\lang.txt (тот же файл, что пишет приложение), иначе культура ОС
/// (ru/uk/be → ru, zh* → zh, прочее → en).</summary>
public static class Strings
{
    public static readonly string Lang = Detect();

    private static string Detect()
    {
        try
        {
            var p = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GodjiVpn", "lang.txt");
            if (File.Exists(p))
            {
                var v = File.ReadAllText(p).Trim().ToLowerInvariant();
                if (v is "ru" or "en" or "zh") return v;
            }
        }
        catch { /* нет доступа — берём культуру ОС */ }

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "ru" or "uk" or "be" => "ru",
            "zh" => "zh",
            _ => "en"
        };
    }

    /// Локализованная строка: T(русский, english, 中文).
    public static string T(string ru, string en, string zh) => Lang switch
    {
        "en" => en,
        "zh" => zh,
        _ => ru
    };

    public static string Mb(long bytes) => Lang switch
    {
        "en" => $"{bytes / 1024 / 1024} MB",
        "zh" => $"{bytes / 1024 / 1024} MB",
        _ => $"{bytes / 1024 / 1024} МБ"
    };
}
