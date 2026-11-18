using System.Diagnostics;
using System.Text;

namespace GodjiVpn.Services;

/// <summary>
/// Автозапуск при входе в систему (Настройки → Подключение). macOS — LaunchAgent
/// xyz.goji.vpn.app в ~/Library/LaunchAgents с RunAtLoad и без KeepAlive: приложение
/// стартует при входе и закрывается как обычное. SMAppService доступен только с macOS 13,
/// а клиент поддерживает 10.15+ — единый механизм LaunchAgents работает везде. launchctl
/// bootout/bootstrap — лучшее усилие: даже если систему не удалось перечитать plist сразу,
/// при следующем входе пользователь получит актуальное поведение. Превью-экземпляр систему
/// не трогает.
/// </summary>
public static class AutoStartService
{
    private const string Label = "xyz.goji.vpn.app";

    private static string PlistPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", Label + ".plist");

    /// <summary>Самопроверка при старте: настройка включена — plist должен указывать на
    /// текущий бандл (путь мог смениться после обновления или переноса приложения).</summary>
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
                Directory.CreateDirectory(Path.GetDirectoryName(PlistPath)!);
                File.WriteAllText(PlistPath, PlistText(exe));
                Bootout();
                try { Run("/bin/launchctl", $"bootstrap gui/{Uid()} '{PlistPath}'"); }
                catch { Run("/bin/launchctl", $"load '{PlistPath}'"); }
            }
            else
            {
                Bootout();
                try { File.Delete(PlistPath); } catch { }
            }
        }
        catch { /* без автозапуска приложение работает как раньше */ }
    }

    private static string PlistText(string exe) => $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0"><dict>
              <key>Label</key><string>{Label}</string>
              <key>ProgramArguments</key><array><string>{exe}</string></array>
              <key>RunAtLoad</key><true/>
            </dict></plist>
            """;

    private static void Bootout()
    {
        try { Run("/bin/launchctl", $"bootout gui/{Uid()}/{Label}"); }
        catch { Run("/bin/launchctl", $"unload '{PlistPath}'"); }
    }

    private static string Uid()
    {
        var output = RunCapture("/usr/bin/id", "-u");
        return output is { Length: > 0 } && output.All(char.IsDigit) ? output : "501";
    }

    private static void Run(string path, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(path, arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true
        });
        process?.WaitForExit(10000);
    }

    private static string? RunCapture(string path, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(path, arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            });
            if (process == null) return null;
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(10000);
            return output;
        }
        catch { return null; }
    }
}
