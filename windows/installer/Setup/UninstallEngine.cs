using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GodjiSetup;

/// <summary>Деинсталляция (unins000.exe, он же этот exe с /uninstall): остановка приложения →
/// ярлыки → ключ реестра → файлы. Пользовательские данные (%LOCALAPPDATA%\GodjiVpn — настройки,
/// подписка, логи) по умолчанию НЕ трогаем, как и прежний Inno-деинсталлятор; удаляются только
/// по явной галочке. Свой exe живёт в удаляемой папке — хвост вычищает отложенный rd.</summary>
public sealed class UninstallEngine
{
    private const string UninstallKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + InstallerEngine.AppId;
    private const string InnoKey = UninstallKey + "_is1";

    public static string UserDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GodjiVpn");

    public async Task RunAsync(SetupVm vm, bool dryRun, IProgress<EngineStep> progress, CancellationToken ct)
    {
        var ownDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        void Phase(string m) => App.LogRun("uninstall: " + m);
        Phase($"begin, dir={ownDir}, dryRun={dryRun}");

        // 0–10%: приложение и ядра.
        progress.Report(new EngineStep("kill", 2));
        if (dryRun) await Task.Delay(700, ct);
        else ProcessGuard.CloseAppAndCores(ownDir, _ => { }, ct);
        progress.Report(new EngineStep("kill", 10));

        // 10–20%: ярлыки (группа в «Пуске» целиком + иконка рабочего стола, если есть).
        progress.Report(new EngineStep("unshortcut", 12));
        if (dryRun) await Task.Delay(300, ct);
        else
        {
            try
            {
                Directory.Delete(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                    InstallerEngine.AppDirName), recursive: true);
            }
            catch (DirectoryNotFoundException) { /* уже нет — ок */ }
            catch (IOException) { /* занят — оставим */ }

            try
            {
                File.Delete(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                    InstallerEngine.AppDirName + ".lnk"));
            }
            catch { /* нет файла — ок */ }
        }
        progress.Report(new EngineStep("unshortcut", 20));

        // 20–30%: реестр — своя запись и, на всякий случай, хвост старого Inno.
        progress.Report(new EngineStep("unreg", 22));
        if (!dryRun)
        {
            DeleteKey(UninstallKey);
            DeleteKey(InnoKey);
        }
        else await Task.Delay(300, ct);
        progress.Report(new EngineStep("unreg", 30));

        // 30–40%: пользовательские данные — только по явной галочке.
        if (vm.DeleteUserData)
        {
            progress.Report(new EngineStep("userdata", 32));
            if (dryRun) await Task.Delay(300, ct);
            else DeleteTreeWithRetry(UserDataDir, attempts: 3);
            progress.Report(new EngineStep("userdata", 40));
        }

        // 40–100%: файлы приложения. Свой exe и файл подкачки сборки пропускаем —
        // их подберёт отложенный rd после нашего выхода.
        if (dryRun)
        {
            await Task.Delay(600, ct);
            progress.Report(new EngineStep("unfiles", 100));
            return;
        }
        var files = Directory.EnumerateFiles(ownDir, "*", SearchOption.AllDirectories)
            .Where(f => !IsSelf(f))
            .Select(f => new FileInfo(f))
            .OrderBy(f => f.Length)
            .ToList();
        long total = files.Sum(f => f.Length), done = 0;
        int locked = 0;
        progress.Report(new EngineStep("unfiles", 40, "0"));
        for (int i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            // Залоченный файл — не ошибка удаления: это почти наверняка нативные библиотеки
            // самого деинсталлятора (WPF single-file резолвит wpfgfx/PresentationNative из
            // СВОЕЙ папки, то есть из папки приложения). Их подберёт отложенный rd после
            // нашего выхода — падать из-за них нельзя.
            try
            {
                DeleteFileWithRetry(files[i].FullName, attempts: 3);
            }
            catch (Exception ex)
            {
                locked++;
                App.LogRun($"uninstall: locked, отложу rd: {files[i].Name} ({ex.GetType().Name})");
            }
            done += files[i].Length;
            progress.Report(new EngineStep("unfiles",
                40 + 58.0 * done / Math.Max(1, total), $"{i + 1} / {files.Count}"));
        }
        App.LogRun($"uninstall: files done, locked={locked}");

        // Хвост: этот exe + залоченные файлы. rd сработает через ~2 с после выхода процесса,
        // когда файлы освободятся.
        bool leftovers = Directory.Exists(ownDir);
        progress.Report(new EngineStep("unfiles", 100));
        if (leftovers)
        {
            App.LogRun("uninstall: spawn delayed rd");
            SpawnDelayedSelfDelete(ownDir);
        }
    }

    private static bool IsSelf(string file)
    {
        var me = Environment.ProcessPath;
        return me != null &&
               string.Equals(Path.GetFullPath(file), Path.GetFullPath(me), StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteTreeWithRetry(string dir, int attempts)
    {
        for (int i = 1; ; i++)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                Directory.Delete(dir, recursive: true);
                return;
            }
            catch (IOException) when (i < attempts) { Thread.Sleep(800); }
            catch (UnauthorizedAccessException) when (i < attempts) { Thread.Sleep(800); }
        }
    }

    private static void DeleteFileWithRetry(string file, int attempts)
    {
        for (int i = 1; ; i++)
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
                return;
            }
            catch (IOException) when (i < attempts) { Thread.Sleep(600); }
            catch (UnauthorizedAccessException) when (i < attempts) { Thread.Sleep(600); }
            catch (FileNotFoundException) { return; }
        }
    }

    private static void DeleteKey(string keyPath)
    {
        try { Microsoft.Win32.Registry.LocalMachine.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false); }
        catch { /* нет ключа — и ладно */ }
        try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false); }
        catch { /* нет ключа — и ладно */ }
    }

    private static void SpawnDelayedSelfDelete(string dir)
    {
        // Цикл с повторами, а не «ping + rd»: окно «Удалено» ещё держит наши нативные DLL
        // (пользователь закроет его когда угодно), поэтому rd повторяется до 10 минут,
        // пока папка не опустеет; если не вышло — остатки доедут при следующей установке.
        var ps = $"for($i=0;$i -lt 600;$i++){{ Remove-Item -LiteralPath '{dir}' -Recurse -Force -ErrorAction SilentlyContinue; if(-not (Test-Path -LiteralPath '{dir}')){{ exit 0 }}; Start-Sleep 1 }}";
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{ps.Replace("\"", "\\\"")}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetTempPath()
        };
        Process.Start(psi);
    }
}
