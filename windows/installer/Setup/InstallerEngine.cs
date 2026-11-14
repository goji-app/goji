using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SevenZip.Compression.LZMA;

namespace GodjiSetup;

/// <summary>Шаг для прогресса: ключ фазы (локализация на стороне UI), 0..100 и деталь
/// (число файлов «214 / 247» или имя проблемного файла).</summary>
public sealed record EngineStep(string PhaseKey, double Percent, string? Files = null, string? Detail = null);

/// <summary>Установка: остановка приложения → тихое удаление старого Inno-установщика →
/// распаковка payload → ярлыки → запись в реестр → (по галочке) запуск. Пишет в
/// %ProgramFiles%\Godji VPN и HKLM\...\Uninstall\{AppId} — наследует место и GUID прежнего
/// Inno-установщика, чтобы обновление поверх старой установки было бесшовным.
/// Payload — контейнер payload.bin (см. installer/PayloadTool): открытый header с числами
/// для таблицы фактов, 5 байт свойств LZMA и LZMA-поток записей (LZMA-SDK, словарь 64 МБ —
/// профиль прежнего Inno lzma2/max). Декодируется во временный файл %TEMP% (DeleteOnClose),
/// затем записи извлекаются последовательно.</summary>
public sealed class InstallerEngine
{
    private const string Magic = "GOJIPAYL";

    public const string AppDirName = "Godji VPN";
    public const string ExeName = "GodjiVpn.exe";
    public const string AppId = "{2E6F1B7A-6C2B-4B7B-9C0A-5D6E0E5A9B41}";
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + AppId;
    private const string HkcuUninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\";

    public static string DefaultInstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppDirName);

    public static string AssemblyVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public sealed record PayloadInfo(int Entries, long Bytes);

    private static Stream? OpenPayloadStream() =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream("GodjiSetup.payload.bin");

    private const int HeaderSize = 8 + 4 + 8 + 8; // magic + count + totalFilesBytes + rawStreamSize
    private const int LzmaPropsSize = 5;

    public static PayloadInfo? ReadPayload()
    {
        var stream = OpenPayloadStream();
        if (stream == null) return null;
        using (stream)
        {
            var header = new byte[HeaderSize];
            if (!ReadExactly(stream, header)) return null;
            if (Encoding.ASCII.GetString(header, 0, 8) != Magic) return null;
            int count = BitConverter.ToInt32(header, 8);
            long total = BitConverter.ToInt64(header, 12);
            return new PayloadInfo(count, total);
        }
    }

    private static bool ReadExactly(Stream s, byte[] buffer)
    {
        int off = 0;
        while (off < buffer.Length)
        {
            int read = s.Read(buffer, off, buffer.Length - off);
            if (read == 0) return false;
            off += read;
        }
        return true;
    }

    public async Task RunAsync(string targetDir, SetupVm vm, bool dryRun,
        IProgress<EngineStep> progress, CancellationToken ct)
    {
        void Phase(string m) => App.LogRun("install: " + m);

        // 0–6%: закрываем приложение и ядра.
        Phase("phase close: begin");
        progress.Report(new EngineStep("close", 1));
        if (dryRun) await Task.Delay(700, ct);
        else ProcessGuard.CloseAppAndCores(targetDir, id => { }, ct);
        Phase("phase close: done");
        progress.Report(new EngineStep("close", 6));

        // 6–10%: старый Inno-установщик (до 1.0.136) — тихий /VERYSILENT деинсталлятор.
        Phase("phase legacy: begin");
        progress.Report(new EngineStep("legacy", 7));
        if (!dryRun) await UninstallLegacyInnoAsync(ct);
        else await Task.Delay(400, ct);
        Phase("phase legacy: done");
        progress.Report(new EngineStep("legacy", 10));

        // 10–92%: payload.
        Phase("phase extract: begin");
        progress.Report(new EngineStep("extract", 10, "0"));
        if (dryRun)
        {
            for (int i = 1; i <= 247; i++)
            {
                await Task.Delay(9, ct);
                progress.Report(new EngineStep("extract", 10 + 82 * i / 247.0, $"{i} / 247"));
            }
        }
        else await ExtractAsync(targetDir, progress, ct);
        Phase("phase extract: done");

        // 92–96%: деинсталлятор + ярлыки.
        progress.Report(new EngineStep("shortcuts", 92));
        if (dryRun)
        {
            await Task.Delay(400, ct);
        }
        else
        {
            // Деинсталлятор — копия самого установщика (он умеет /uninstall и /quiet).
            // Отдельный exe в payload не кладём: ~60 МБ уже сжатого кода легли бы туда
            // почти без сжатия и раздули установщик.
            Phase("unins000: copying self");
            var self = Environment.ProcessPath
                ?? throw new InvalidOperationException("Не удалось определить путь установщика");
            var uninsPath = Path.Combine(targetDir, "unins000.exe");
            File.Copy(self, uninsPath, overwrite: true);
            try
            {
                // У скачанного установщика может быть Mark-of-the-Web; копия наследует его,
                // а реестр потом запускает unins000.exe — снимаем пометку.
                File.Delete(uninsPath + ":Zone.Identifier");
            }
            catch { /* пометки нет — ок */ }

            Phase("shortcuts: begin");
            CreateShortcuts(targetDir, vm.DesktopShortcut);
            Phase("shortcuts: done");
        }
        progress.Report(new EngineStep("shortcuts", 96));

        // 96–100%: реестр.
        Phase("phase registry: begin");
        progress.Report(new EngineStep("registry", 97));
        if (dryRun) await Task.Delay(300, ct);
        else WriteUninstallRegistry(targetDir);
        Phase("phase registry: done");
        progress.Report(new EngineStep("registry", 100));
    }

    public static void Launch(string targetDir) => Process.Start(new ProcessStartInfo
    {
        FileName = Path.Combine(targetDir, ExeName),
        UseShellExecute = true // манифест requireAdministrator учитывается; установщик уже elevated — без второго UAC
    });

    // ── распаковка payload.bin ────────────────────────────────────────────

    private async Task ExtractAsync(string targetDir, IProgress<EngineStep> progress, CancellationToken ct)
    {
        using var resource = OpenPayloadStream()
            ?? throw new InvalidOperationException("payload.bin не встроен в сборку");

        var header = new byte[HeaderSize];
        if (!ReadExactly(resource, header) || Encoding.ASCII.GetString(header, 0, 8) != Magic)
            throw new InvalidDataException("Повреждён встроенный payload");
        int count = BitConverter.ToInt32(header, 8);
        long totalFilesBytes = BitConverter.ToInt64(header, 12);
        long rawStreamSize = BitConverter.ToInt64(header, 20);
        App.LogRun($"extract: entries={count} filesBytes={totalFilesBytes} raw={rawStreamSize}");

        // LZMA-декод (push-модель SDK) во временный файл, затем последовательный разбор
        // записей из него. Как у Inno: распаковка в %TEMP%, только файлы без архивного каркаса.
        var props = new byte[LzmaPropsSize];
        ReadExactly(resource, props);
        long compressedSize = resource.Length - HeaderSize - LzmaPropsSize;
        App.LogRun($"extract: resource len={resource.Length} compressed={compressedSize}");

        var tempPath = Path.Combine(Path.GetTempPath(), $"GodjiSetup-payload-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var temp = File.Create(tempPath, 1024 * 1024, FileOptions.DeleteOnClose))
            {
                var decoder = new SevenZip.Compression.LZMA.Decoder();
                decoder.SetDecoderProperties(props);
                App.LogRun("extract: lzma decode begin");
                // Декодирование — самый долгий шаг; отпускаем UI-поток (вызов из Task.Run).
                decoder.Code(resource, temp, compressedSize, rawStreamSize, null);
                App.LogRun($"extract: lzma decode done, temp={temp.Length}");
                temp.Position = 0;

                var destRoot = Path.GetFullPath(targetDir).TrimEnd(Path.DirectorySeparatorChar)
                               + Path.DirectorySeparatorChar;
                Directory.CreateDirectory(destRoot);

                using var br = new BinaryReader(temp, Encoding.UTF8, leaveOpen: true);
                long doneBytes = 0;
                var buffer = new byte[1024 * 1024];
                for (int i = 0; i < count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    int pathLen = br.ReadInt32();
                    string rel = Encoding.UTF8.GetString(br.ReadBytes(pathLen));
                    long lastWriteTicks = br.ReadInt64();
                    long dataLen = br.ReadInt64();

                    string dest = SafePath(destRoot, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    if (i % 50 == 0) App.LogRun($"extract: file {i + 1}/{count} {rel}");
                    await ExtractFileAsync(temp, dest, dataLen, lastWriteTicks, buffer, ct);

                    doneBytes += dataLen;
                    double pct = 10 + 82.0 * doneBytes / Math.Max(1, totalFilesBytes);
                    progress.Report(new EngineStep("extract", pct, $"{i + 1} / {count}"));
                }
            }
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* уже удалён или занят — TEMP чистится сам */ }
        }
    }

    private static string SafePath(string root, string rel)
    {
        // Защита от «ухода» пути из архива за пределы папки установки.
        var full = Path.GetFullPath(Path.Combine(root, rel));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Некорректный путь в payload: " + rel);
        return full;
    }

    private static async Task ExtractFileAsync(Stream source, string dest, long length, long lastWriteTicks,
        byte[] buffer, CancellationToken ct)
    {
        // Файл мог остаться занятым (ядро, антивирус, гонка с деинсталлятором старой версии) —
        // 5 попыток с паузой: обычно достаточно одной.
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await using (var fs = new FileStream(dest, FileMode.Create, FileAccess.Write,
                    FileShare.None, 81920, useAsync: true))
                {
                    long left = length;
                    while (left > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        int chunk = (int)Math.Min(buffer.Length, left);
                        await source.ReadExactlyAsync(buffer.AsMemory(0, chunk), ct);
                        await fs.WriteAsync(buffer.AsMemory(0, chunk), ct);
                        left -= chunk;
                    }
                }
                File.SetLastWriteTimeUtc(dest, new DateTime(lastWriteTicks, DateTimeKind.Utc));
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                await Task.Delay(1000, ct);
            }
        }
    }

    // ── ярлыки ────────────────────────────────────────────────────────────

    private void CreateShortcuts(string targetDir, bool desktopShortcut)
    {
        var exe = Path.Combine(targetDir, ExeName);
        var group = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), AppDirName);
        Directory.CreateDirectory(group);
        CreateShortcut(Path.Combine(group, AppDirName + ".lnk"), exe);

        if (desktopShortcut)
            CreateShortcut(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                AppDirName + ".lnk"), exe);
    }

    private static void CreateShortcut(string lnkPath, string target)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell")
                   ?? throw new InvalidOperationException("WScript.Shell недоступен");
        dynamic shell = Activator.CreateInstance(type)!;
        try
        {
            dynamic lnk = shell.CreateShortcut(lnkPath);
            lnk.TargetPath = target;
            lnk.WorkingDirectory = Path.GetDirectoryName(target)!;
            lnk.IconLocation = target + ",0";
            lnk.Save();
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }

    // ── реестр ────────────────────────────────────────────────────────────

    private static void WriteUninstallRegistry(string targetDir)
    {
        using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(UninstallKey, writable: true);
        var exe = Path.Combine(targetDir, ExeName);
        var unins = Path.Combine(targetDir, "unins000.exe");
        long bytes = 0;
        try
        {
            foreach (var f in Directory.EnumerateFiles(targetDir, "*", SearchOption.AllDirectories))
                bytes += new FileInfo(f).Length;
        }
        catch { /* размер приблизительный — не критично */ }

        key.SetValue("DisplayName", "Godji VPN");
        key.SetValue("DisplayVersion", AssemblyVersion);
        key.SetValue("Publisher", "Godji");
        key.SetValue("InstallLocation", targetDir);
        key.SetValue("DisplayIcon", exe);
        key.SetValue("UninstallString", $"\"{unins}\" /uninstall");
        key.SetValue("QuietUninstallString", $"\"{unins}\" /uninstall /quiet");
        key.SetValue("EstimatedSize", (uint)Math.Max(1, bytes / 1024), Microsoft.Win32.RegistryValueKind.DWord);
        key.SetValue("NoModify", 1, Microsoft.Win32.RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, Microsoft.Win32.RegistryValueKind.DWord);
    }

    // ── старый Inno-установщик ────────────────────────────────────────────

    private static async Task UninstallLegacyInnoAsync(CancellationToken ct)
    {
        var exe = FindLegacyUninstaller(Microsoft.Win32.Registry.LocalMachine, UninstallKey + "_is1")
               ?? FindLegacyUninstaller(Microsoft.Win32.Registry.CurrentUser, HkcuUninstallKey + InstallerEngine.AppId + "_is1");
        if (exe == null) return;

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
            UseShellExecute = true,
            CreateNoWindow = true
        };
        try
        {
            using var p = Process.Start(psi);
            if (p != null)
                await p.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(180), ct);
        }
        catch
        {
            // Старый деинсталлятор мог не запуститься (битая установка) — ключ подчистим ниже.
        }

        // unins000.exe перекидывает себя в %TEMP% и выходит раньше фактического удаления —
        // ждём исчезновения ключа, затем дожимаем обе ветки реестра принудительно.
        for (int i = 0; i < 30 && KeyExists(UninstallKey + "_is1"); i++)
            await Task.Delay(1000, ct);
        DeleteKeyIfExists(UninstallKey + "_is1");
        DeleteKeyIfExists(HkcuUninstallKey + AppId + "_is1");
    }

    private static string? FindLegacyUninstaller(Microsoft.Win32.RegistryKey root, string keyPath)
    {
        using var key = root.OpenSubKey(keyPath);
        if (key != null)
        {
            var quiet = key.GetValue("QuietUninstallString") as string;
            var plain = key.GetValue("UninstallString") as string;
            return ParseExe(quiet) ?? ParseExe(plain);
        }
        return null;
    }

    /// UninstallString приходит вида «"C:\Program Files\Godji VPN\unins000.exe"» — вырезаем exe.
    private static string? ParseExe(string? cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd)) return null;
        cmd = cmd.TrimStart();
        if (cmd.StartsWith("\""))
        {
            int close = cmd.IndexOf('"', 1);
            return close > 0 ? cmd[1..close] : null;
        }
        int sp = cmd.IndexOf(' ');
        return sp > 0 ? cmd[..sp] : cmd;
    }

    private static bool KeyExists(string keyPath) =>
        Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath) != null ||
        Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath) != null;

    private static void DeleteKeyIfExists(string keyPath)
    {
        try { Microsoft.Win32.Registry.LocalMachine.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false); }
        catch { /* нет ключа — и ладно */ }
        try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false); }
        catch { /* нет ключа — и ладно */ }
    }
}
