using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using GodjiVpn.Utils;

namespace GodjiVpn.Services;

/// <summary>Сортировка списка серверов — порт Android ServerSort (SettingsRepository).</summary>
public enum ServerSort { Favorites, Ping, Name }

/// <summary>Режим Per-app Proxy — порт Android PerAppProxyMode (SettingsRepository):
/// Off — прокси применяется ко всем программам без исключений; Allow — через VPN идут
/// ТОЛЬКО выбранные, остальное напрямую; Bypass — выбранные идут напрямую, остальное через VPN.</summary>
public enum PerAppProxyMode { Off, Allow, Bypass }

/// <summary>
/// Мелкие пользовательские настройки приложения, переживающие перезапуск (аналог
/// SettingsRepository в Android): %LOCALAPPDATA%\GodjiVpn\app-settings.json. Отдельные файлы
/// уже существующих настроек (тема, пинг, избранное) не переносятся сюда — чтобы не ломать
/// сохранённый выбор при обновлении.
/// </summary>
public sealed class AppSettings
{
    private static string StatePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GodjiVpn", "app-settings.json");

    public sealed class State
    {
        public ServerSort ServerSort { get; set; } = ServerSort.Favorites;
        /// <summary>Размер шрифта (Настройки → Внешний вид), как FontSizePreset в Android.</summary>
        public FontSizePreset FontSize { get; set; } = FontSizePreset.Normal;
        /// <summary>"Сайты мимо VPN" — домены в ASCII (punycode), без www., с поддоменами.</summary>
        public List<string> BypassDomains { get; set; } = new();
        /// <summary>Правила Wi-Fi (NetworkRulesManager).</summary>
        public bool WifiAutoConnect { get; set; }
        public bool WifiDisconnectTrusted { get; set; }
        public List<string> TrustedSsids { get; set; } = new();
        /// <summary>Kill switch: при сбое ядра туннель не снимается — интернет закрыт, пока
        /// ядро перезапускается (порт Android-настройки «Kill switch»).</summary>
        public bool KillSwitch { get; set; }
        /// <summary>Новогоднее оформление (1 декабря — 31 января); включено по умолчанию.</summary>
        public bool WinterDecor { get; set; } = true;
        /// <summary>«Приложения мимо VPN» — имена исполняемых файлов (chrome.exe, Safari).
        /// Теперь это список Per-app Proxy: в режиме Bypass — программы в обход VPN, в режиме
        /// Allow — только они и идут через VPN.</summary>
        public List<string> BypassApps { get; set; } = new();
        /// <summary>Режим Per-app Proxy ("OFF"/"ALLOW"/"BYPASS") — строкой, чтобы файл настроек
        /// читался и старыми версиями. Пусто = не выбрано (см. миграцию в Load()).</summary>
        public string PerAppProxy { get; set; } = "";
        /// <summary>Блокировка рекламы: домены geosite:category-ads-all идут в blackhole.</summary>
        public bool BlockAds { get; set; }
        /// <summary>Автозапуск приложения при входе в систему (см. AutoStartService).</summary>
        public bool StartWithSystem { get; set; }
        /// <summary>Подключаться к последнему узлу при старте приложения.</summary>
        public bool AutoConnectOnLaunch { get; set; }
        /// <summary>Момент последнего успешного обновления гео-баз (ISO), пусто — никогда.</summary>
        public string GeoUpdatedUtc { get; set; } = "";
    }

    private readonly State _state;

    public event Action? Changed;

    public AppSettings() => _state = Load();

    public ServerSort ServerSort
    {
        get => _state.ServerSort;
        set { if (_state.ServerSort == value) return; _state.ServerSort = value; Save(); }
    }

    public FontSizePreset FontSize
    {
        get => _state.FontSize;
        set { if (_state.FontSize == value) return; _state.FontSize = value; Save(); }
    }

    public IReadOnlyList<string> BypassDomains => _state.BypassDomains;

    public bool WifiAutoConnect
    {
        get => _state.WifiAutoConnect;
        set { if (_state.WifiAutoConnect == value) return; _state.WifiAutoConnect = value; Save(); }
    }

    public bool WifiDisconnectTrusted
    {
        get => _state.WifiDisconnectTrusted;
        set { if (_state.WifiDisconnectTrusted == value) return; _state.WifiDisconnectTrusted = value; Save(); }
    }

    public IReadOnlyList<string> TrustedSsids => _state.TrustedSsids;

    public bool WinterDecor
    {
        get => _state.WinterDecor;
        set { if (_state.WinterDecor == value) return; _state.WinterDecor = value; Save(); }
    }

    public bool KillSwitch
    {
        get => _state.KillSwitch;
        set { if (_state.KillSwitch == value) return; _state.KillSwitch = value; Save(); }
    }

    public IReadOnlyList<string> BypassApps => _state.BypassApps;

    public bool BlockAds
    {
        get => _state.BlockAds;
        set { if (_state.BlockAds == value) return; _state.BlockAds = value; Save(); }
    }

    public bool StartWithSystem
    {
        get => _state.StartWithSystem;
        set { if (_state.StartWithSystem == value) return; _state.StartWithSystem = value; Save(); }
    }

    public bool AutoConnectOnLaunch
    {
        get => _state.AutoConnectOnLaunch;
        set { if (_state.AutoConnectOnLaunch == value) return; _state.AutoConnectOnLaunch = value; Save(); }
    }

    public string GeoUpdatedUtc
    {
        get => _state.GeoUpdatedUtc;
        set { if (_state.GeoUpdatedUtc == value) return; _state.GeoUpdatedUtc = value; Save(); }
    }

    public PerAppProxyMode PerAppMode
    {
        get => _state.PerAppProxy.ToUpperInvariant() switch
        {
            "ALLOW" => PerAppProxyMode.Allow,
            "BYPASS" => PerAppProxyMode.Bypass,
            _ => PerAppProxyMode.Off
        };
        set
        {
            var code = value switch
            {
                PerAppProxyMode.Allow => "ALLOW",
                PerAppProxyMode.Bypass => "BYPASS",
                _ => "OFF"
            };
            if (_state.PerAppProxy == code) return;
            _state.PerAppProxy = code;
            Save();
        }
    }

    public void AddBypassApp(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName) || _state.BypassApps.Contains(processName, StringComparer.OrdinalIgnoreCase)) return;
        _state.BypassApps.Add(processName);
        _state.BypassApps.Sort(StringComparer.OrdinalIgnoreCase);
        Save();
    }

    public void RemoveBypassApp(string processName)
    {
        if (_state.BypassApps.Remove(processName)) Save();
    }

    public void AddTrustedSsid(string ssid)
    {
        if (string.IsNullOrEmpty(ssid) || _state.TrustedSsids.Contains(ssid)) return;
        _state.TrustedSsids.Add(ssid);
        _state.TrustedSsids.Sort(StringComparer.CurrentCultureIgnoreCase);
        Save();
    }

    public void RemoveTrustedSsid(string ssid)
    {
        if (_state.TrustedSsids.Remove(ssid)) Save();
    }

    public void AddBypassDomain(string domain)
    {
        if (_state.BypassDomains.Contains(domain)) return;
        _state.BypassDomains.Add(domain);
        _state.BypassDomains.Sort(StringComparer.Ordinal);
        Save();
    }

    public void RemoveBypassDomain(string domain)
    {
        if (_state.BypassDomains.Remove(domain)) Save();
    }

    private static readonly IdnMapping Idn = new();
    private static readonly Regex DomainRegex = new(@"^[a-z0-9-]+(\.[a-z0-9-]+)+$", RegexOptions.Compiled);

    /// <summary>Порт SettingsRepository.normalizeDomain (Android): https://www.site.ru/path →
    /// site.ru, кириллица → punycode; null — если это не похоже на адрес сайта.</summary>
    public static string? NormalizeDomain(string input)
    {
        var d = input.Trim().ToLowerInvariant();
        var scheme = d.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0) d = d[(scheme + 3)..];
        foreach (var stop in new[] { '/', '?', '#', ':' })
        {
            var i = d.IndexOf(stop);
            if (i >= 0) d = d[..i];
        }
        if (d.StartsWith("*.")) d = d[2..];
        if (d.StartsWith('.')) d = d[1..];
        if (d.StartsWith("www.")) d = d[4..];
        d = d.TrimEnd('.');
        if (d.Length == 0) return null;
        string ascii;
        try { ascii = Idn.GetAscii(d); } catch { return null; }
        return DomainRegex.IsMatch(ascii) ? ascii : null;
    }

    /// <summary>Для показа: xn--… → кириллица.</summary>
    public static string ToDisplay(string domain)
    {
        try { return Idn.GetUnicode(domain); } catch { return domain; }
    }

    private static State Load()
    {
        try
        {
            var state = File.Exists(StatePath)
                ? JsonSerializer.Deserialize<State>(File.ReadAllText(StatePath)) ?? new State()
                : new State();
            // Раньше списков с режимами не было: непустой список означал именно обход. Разовый
            // перенос, чтобы обновившиеся пользователи не потеряли прежнее поведение.
            if (string.IsNullOrEmpty(state.PerAppProxy) && state.BypassApps.Count > 0)
                state.PerAppProxy = "BYPASS";
            return state;
        }
        catch { return new State(); }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
            File.WriteAllText(StatePath, JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* лучшее усилие — при сбое записи настройка просто не переживёт перезапуск */ }
        Changed?.Invoke();
    }
}
