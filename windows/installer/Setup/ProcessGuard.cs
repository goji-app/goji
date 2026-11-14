using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace GodjiSetup;

/// <summary>Остановка запущенного приложения и его ядер (xray/sing-box) перед установкой/удалением.
/// Приложение стараемся закрыть по-хорошему (WM_CLOSE → штатный teardown туннеля), ядра — только
/// по ПОЛНОМУ пути из нашей папки: taskkill по имени образа убил бы и ядро чужого v2ray-клиента.</summary>
public static class ProcessGuard
{
    public const string AppProcessName = "GodjiVpn";
    private static readonly string[] CoreNames = { "xray", "sing-box" };

    public static void CloseAppAndCores(string dir, Action<string>? log, CancellationToken ct)
    {
        CloseApp(log, ct);
        KillCores(dir);
    }

    public static void CloseApp(Action<string>? log, CancellationToken ct)
    {
        foreach (var p in Process.GetProcessesByName(AppProcessName))
        {
            using (p)
            {
                try
                {
                    log?.Invoke(p.Id.ToString());
                    // Окно может быть скрыто в трей (MainWindowHandle == 0) — тогда штатного
                    // закрытия не будет, останется Kill. Туннель при этом умирает вместе с
                    // процессом; осиротевший фантомный адаптер приложение чистит само при
                    // следующем connect (превентивная чистка 3082b04).
                    if (p.MainWindowHandle != 0 && p.CloseMainWindow())
                    {
                        if (p.WaitForExit((int)TimeSpan.FromSeconds(12).TotalMilliseconds)) continue;
                        // 12 с — больше, чем штатный teardown туннеля; дальше не ждём.
                    }
                    if (!p.HasExited) p.Kill(entireProcessTree: true);
                    p.WaitForExit(5000);
                }
                catch
                {
                    // Процесс мог выйти сам между проверками — не ошибка.
                }
            }
        }
        ct.ThrowIfCancellationRequested();
    }

    public static void KillCores(string dir)
    {
        dir = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var name in CoreNames)
            foreach (var p in Process.GetProcessesByName(name))
            {
                using (p)
                {
                    try
                    {
                        var path = p.MainModule?.FileName;
                        if (path != null &&
                            path.StartsWith(dir, StringComparison.OrdinalIgnoreCase) &&
                            !p.HasExited)
                            p.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // Чужое ядро с тем же именем или уже умерло — не трогаем.
                    }
                }
            }
    }
}
