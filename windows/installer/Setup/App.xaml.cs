using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace GodjiSetup;

/// Точка входа установщика/деинсталлятора Godji VPN.
/// Один и тот же exe работает в двух ролях:
///   • установка  — просто запущен (имя GodjiSetup.exe);
///   • деинсталляция — лежит в папке приложения как unins000.exe и/или запущен с /uninstall.
/// Флаги: /uninstall — режим удаления, /quiet — без UI (для тихого удаления).
public partial class App : Application
{
    public static bool UninstallMode { get; private set; }
    public static bool Quiet { get; private set; }

    /// Краш-лог в %TEMP%: у установщика нет окна логов, а разбирать чужие «установщик не открылся»
    /// без текста исключения невозможно. Файл молча пересоздаётся/дописывается — не критичная часть.
    public static void LogCrash(Exception ex)
    {
        var root = ex.GetBaseException() ?? ex;
        try
        {
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "GodjiSetup-crash.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {root.GetType().Name}: {root.Message}\r\n{root.StackTrace}\r\n\r\n");
        }
        catch { /* некуда писать — и ладно */ }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var args = e.Args;
        foreach (var a in args)
        {
            var low = a.ToLowerInvariant();
            if (low is "/uninstall" or "-uninstall" or "--uninstall") UninstallMode = true;
            if (low is "/quiet" or "-quiet" or "--quiet" or "/q" or "-q") Quiet = true;
        }
        // unins000.exe — имя деинсталлятора в папке приложения; реестр зовёт его с /uninstall,
        // но и прямой запуск без аргументов должен означать удаление, а не установку.
        if (AppDomain.CurrentDomain.FriendlyName.Equals("unins000.exe", StringComparison.OrdinalIgnoreCase))
            UninstallMode = true;

        DispatcherUnhandledException += (s, ea) =>
        {
            LogCrash(ea.Exception);
            if (MainWindow is MainWindow mw) mw.ReportCrash(ea.Exception);
            ea.Handled = true;
        };

        MainWindow window;
        try
        {
            window = new MainWindow();
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            MessageBox.Show($"{ex.GetType().Name}: {ex.Message}", "Godji VPN Setup",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        MainWindow = window;
        if (Quiet)
        {
            // Тихий режим: окна нет, деинсталляция исполняется сразу; код 0 — успех.
            try
            {
                await window.RunQuietUninstallAsync();
                Shutdown(0);
            }
            catch
            {
                Shutdown(1);
            }
            return;
        }
        window.Show();
    }
}
