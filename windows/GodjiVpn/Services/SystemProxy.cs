using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GodjiVpn.Services;

/// <summary>
/// Системный прокси Windows для режима «прокси без туннеля»: Internet Settings текущего
/// пользователя (HKCU) + уведомление WinINET, чтобы приложения подхватили без перезапуска.
/// Прописываем socks=…: наш xray слушает только SOCKS, HTTP-прокси на этот порт нельзя.
/// Прежние значения (включая PAC) сохраняются в файл и возвращаются при отключении —
/// пользователь мог жить со своим прокси/PAC, молча затирать их нельзя. Файл нужен и на
/// случай падения приложения в прокси-режиме: при следующем старте с выключенной настройкой
/// сохранённое состояние восстанавливается, иначе после сбоя у пользователя остался бы
/// мёртвый прокси и «не работает интернет».
/// </summary>
public static class SystemProxy
{
    private const string KeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings";

    private sealed class Saved
    {
        public uint? ProxyEnable { get; set; }
        public string? ProxyServer { get; set; }
        public string? ProxyOverride { get; set; }
        public string? AutoConfigUrl { get; set; }
        [JsonPropertyName("AppliedProxyServer")]
        public string? AppliedProxyServer { get; set; }
    }

    private static string BackupPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GodjiVpn", "proxy-backup.json");

    /// <summary>Включает прокси на локальный SOCKS xray. Повторный вызов (переподключение,
    /// самопроверка после сбоя) не затирает сохранённое состояние: бэкап пишется только если
    /// его ещё нет — он должен описывать состояние ДО первого включения.</summary>
    public static void Enable(int socksPort)
    {
        var applied = $"socks=127.0.0.1:{socksPort}";
        var backupPath = BackupPath;
        if (!File.Exists(backupPath))
        {
            var saved = new Saved
            {
                ProxyEnable = GetDword("ProxyEnable"),
                ProxyServer = GetString("ProxyServer"),
                ProxyOverride = GetString("ProxyOverride"),
                AutoConfigUrl = GetString("AutoConfigURL"),
                AppliedProxyServer = applied
            };
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                File.WriteAllText(backupPath, JsonSerializer.Serialize(saved));
            }
            catch { /* не сохранили — при отключении просто выключим прокси */ }
        }

        using var key = OpenKey(true)!;
        key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
        key.SetValue("ProxyServer", applied, RegistryValueKind.String);
        // PAC несовместим с нашим прокси: пока режим включён, скрипт автонастройки убираем
        // (сохранён в бэкапе). ProxyOverride не трогаем: пользовательские исключения
        // (локальные адреса мимо прокси) имеют смысл и с нашим socks.
        try { key.DeleteValue("AutoConfigURL", false); } catch { }
        Refresh();
    }

    /// <summary>Возвращает прежние настройки прокси (или выключает, если сохранять было
    /// нечего). Идемпотентно: повторный вызов и вызов без включённого режима безопасны.</summary>
    public static void Disable()
    {
        Saved? saved = null;
        try
        {
            var backupPath = BackupPath;
            if (File.Exists(backupPath))
                saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(backupPath));
        }
        catch { }

        using var key = OpenKey(true)!;
        if (saved != null)
        {
            if (saved.ProxyEnable is { } enable) key.SetValue("ProxyEnable", enable, RegistryValueKind.DWord);
            else key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
            SetOrDelete(key, "ProxyServer", saved.ProxyServer);
            SetOrDelete(key, "ProxyOverride", saved.ProxyOverride);
            SetOrDelete(key, "AutoConfigURL", saved.AutoConfigUrl);
        }
        else
        {
            key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
        }
        Refresh();
        try { if (File.Exists(BackupPath)) File.Delete(BackupPath); } catch { }
    }

    /// <summary>Самопроверка при старте: бэкап есть, а прокси-режим выключен — значит прошлая
    /// сессия упала, не вернув настройки пользователя. Восстанавливаем молча.</summary>
    public static void RestoreIfOrphaned(bool proxyModeEnabled)
    {
        if (proxyModeEnabled) return;
        if (!File.Exists(BackupPath)) return;
        Disable();
    }

    private static void SetOrDelete(RegistryKey key, string name, string? value)
    {
        if (string.IsNullOrEmpty(value)) { try { key.DeleteValue(name, false); } catch { } }
        else key.SetValue(name, value, RegistryValueKind.String);
    }

    private static RegistryKey? OpenKey(bool writable)
    {
        try
        {
            return Registry.CurrentUser.OpenSubKey(KeyPath, writable);
        }
        catch { return null; }
    }

    private static uint? GetDword(string name)
    {
        try
        {
            using var key = OpenKey(false);
            var value = key?.GetValue(name);
            return value is int i ? (uint)i : null;
        }
        catch { return null; }
    }

    private static string? GetString(string name)
    {
        try
        {
            using var key = OpenKey(false);
            return key?.GetValue(name) as string;
        }
        catch { return null; }
    }

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr hInternet, int option, IntPtr buffer, int bufferLength);

    /// <summary>Без этих двух опций приложения узнают о смене прокси только после своего
    /// перезапуска — для «включил тумблер и сразу работает» нужны SETTINGS_CHANGED + REFRESH.</summary>
    private static void Refresh()
    {
        InternetSetOption(IntPtr.Zero, 39 /* INTERNET_OPTION_SETTINGS_CHANGED */, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, 37 /* INTERNET_OPTION_REFRESH */, IntPtr.Zero, 0);
    }
}
