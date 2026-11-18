using System.Diagnostics;

namespace GodjiVpn.Services;

/// <summary>
/// Автозапуск при входе в систему (Настройки → Подключение). Windows — задача планировщика
/// с /RL HIGHEST: приложение требует прав администратора (TUN через Wintun), обычный
/// Run-ключ реестра запустил бы его без прав, и VPN не смог бы подняться. Задача
/// пересоздаётся при каждом старте с включённой настройкой — путь к exe после обновления
/// приложения подхватывается сам. Превью-экземпляр систему не трогает: он запущен без прав
/// и рядом с установленным клиентом.
/// </summary>
public static class AutoStartService
{
    private const string TaskName = "GodjiVpn Autostart";

    /// <summary>Самопроверка при старте: настройка включена — задача должна существовать и
    /// указывать на текущий exe. Ошибки не мешают запуску приложения.</summary>
    public static void SyncOnStartup(bool enabled)
    {
        if (enabled) SetEnabled(true);
    }

    public static void SetEnabled(bool enabled)
    {
        if (Environment.GetEnvironmentVariable("GODJI_UI_PREVIEW") == "1") return;
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return;
        try
        {
            if (enabled)
            {
                var run = $"/Create /F /TN \"{TaskName}\" /TR \"\\\"{exe}\\\"\" /SC ONLOGON /RL HIGHEST";
                Run(run);
            }
            else
            {
                Run($"/Delete /F /TN \"{TaskName}\"");
            }
        }
        catch { /* без автозапуска приложение работает как раньше */ }
    }

    private static void Run(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("schtasks", arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false
        });
        process?.WaitForExit(15000);
    }
}
