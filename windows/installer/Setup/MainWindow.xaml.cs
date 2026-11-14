using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace GodjiSetup;

/// <summary>Окно установщика/деинсталлятора. Собственная рамка (титлбар в разметке),
/// скругление через DWM; состояние экрана держит SetupVm, работу — InstallerEngine/UninstallEngine.
/// Для отладки UI: GODJI_SETUP_DRYRUN=1 — двигатели имитируют работу без единой записи на диск.</summary>
public partial class MainWindow : Window
{
    private readonly SetupVm _vm = new();
    private readonly InstallerEngine _installer = new();
    private readonly UninstallEngine _uninstaller = new();
    private CancellationTokenSource? _cts;
    private string _installDir = InstallerEngine.DefaultInstallDir;
    private bool _dryRun;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Win32.TryRoundCorners(this);

        _dryRun = Environment.GetEnvironmentVariable("GODJI_SETUP_DRYRUN") == "1";
        // Отладочные хуки: GODJI_SETUP_AUTO=1 — авто-старт установки, =uninstall — экран
        // подтверждения удаления, =uninstall-go — авто-старт удаления. Только для скриншотов UI.
        var auto = Environment.GetEnvironmentVariable("GODJI_SETUP_AUTO");
        bool uninstallMode = App.UninstallMode || auto == "uninstall" || auto == "uninstall-go";
        _vm.ScreenMode = uninstallMode ? SetupVm.Mode.Uninstall : SetupVm.Mode.Install;
        _installDir = ResolveInstallDir(uninstallMode);

        if (Environment.OSVersion.Version.Major < 10)
        {
            ShowError(Strings.T("Нужна Windows 10 (x64) или новее.",
                "Windows 10 (x64) or newer is required.",
                "需要 Windows 10（x64）或更高版本。"));
            return;
        }

        FillFacts();
        ApplyState();
        if (auto == "1" || auto == "uninstall-go")
        {
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            t.Tick += (_, _) => { t.Stop(); if (auto == "1") StartInstall(); else StartUninstall(); };
            t.Start();
        }
        Closed += (s, e) =>
        {
            _cts?.Cancel();
            if (!App.Quiet) Application.Current.Shutdown();
        };
    }

    private static string ResolveInstallDir(bool uninstallMode)
    {
        if (uninstallMode)
            return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        // Если старая Inno-установка жила не в дефолтной папке — обновляем её место,
        // иначе ставим в стандартный Program Files\Godji VPN (как прежний {autopf}).
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + InstallerEngine.AppId + "_is1");
            if (key?.GetValue("InstallLocation") is string loc &&
                Directory.Exists(loc) && File.Exists(Path.Combine(loc, InstallerEngine.ExeName)))
                return loc;
        }
        catch { /* нет ключа — дефолт */ }
        return InstallerEngine.DefaultInstallDir;
    }

    // ── факты для instrument-таблицы ──────────────────────────────────────

    private void FillFacts()
    {
        _vm.VersionValue = InstallerEngine.AssemblyVersion;
        _vm.PathValue = _installDir;
        _vm.SystemValue = "WINDOWS 10+ · X64";
        _vm.FilesValue = "—";
        _vm.SizeValue = "—";

        if (App.UninstallMode)
        {
            long bytes = 0; int files = 0;
            try
            {
                foreach (var f in Directory.EnumerateFiles(_installDir, "*", SearchOption.AllDirectories))
                {
                    files++;
                    bytes += new FileInfo(f).Length;
                }
            }
            catch { /* папки может уже не быть */ }
            _vm.SizeValue = bytes > 0 ? Strings.Mb(bytes) : "—";
            _vm.FilesValue = files > 0 ? files.ToString() : "—";
            return;
        }

        if (_dryRun)
        {
            _vm.SizeValue = Strings.Mb(93 * 1024L * 1024L);
            _vm.FilesValue = "247";
            return;
        }

        var payload = InstallerEngine.ReadPayload();
        if (payload != null)
        {
            _vm.SizeValue = Strings.Mb(payload.Bytes);
            _vm.FilesValue = payload.Entries.ToString();
        }
    }

    // ── локализация экрана ────────────────────────────────────────────────

    private string T(string ru, string en, string zh) => Strings.T(ru, en, zh);

    private string PhaseText(string key) => key switch
    {
        "close" or "kill" => T("ОСТАНАВЛИВАЕМ ПРИЛОЖЕНИЕ", "STOPPING THE APP", "正在关闭应用"),
        "legacy" => T("ЧИСТИМ СТАРУЮ УСТАНОВКУ", "REMOVING OLD SETUP", "清理旧安装"),
        "extract" => T("РАСПАКОВКА ФАЙЛОВ", "EXTRACTING FILES", "解压文件"),
        "shortcuts" => T("ЯРЛЫКИ", "SHORTCUTS", "快捷方式"),
        "registry" => T("РЕЕСТР", "REGISTRY", "注册表"),
        "unshortcut" => T("УДАЛЯЕМ ЯРЛЫКИ", "REMOVING SHORTCUTS", "删除快捷方式"),
        "userdata" => T("НАСТРОЙКИ", "USER DATA", "用户数据"),
        "unfiles" => T("УДАЛЯЕМ ФАЙЛЫ", "DELETING FILES", "删除文件"),
        _ => key.ToUpperInvariant()
    };

    private string ModeWord() => _vm.ScreenMode == SetupVm.Mode.Install
        ? T("УСТАНОВКА", "SETUP", "安装")
        : T("УДАЛЕНИЕ", "UNINSTALL", "卸载");

    // ── состояние экрана ──────────────────────────────────────────────────

    private void ApplyState()
    {
        TitleText.Text = $"GOJI VPN · {InstallerEngine.AssemblyVersion}" + (_dryRun ? " · DRY-RUN" : "");
        _vm.DesktopCheckText = T("Ярлык на рабочем столе", "Desktop shortcut", "创建桌面快捷方式");
        _vm.LaunchCheckText = T("Запустить после установки", "Launch after setup", "安装后启动应用");
        _vm.UserDataCheckText = T("Удалить и настройки (логи, подписка)",
            "Also delete settings (logs, subscription)", "同时删除设置（日志、订阅）");
        LblVersion.Text = T("ВЕРСИЯ", "VERSION", "版本");
        LblPath.Text = T("ПУТЬ", "PATH", "路径");
        LblSize.Text = T("РАЗМЕР", "SIZE", "大小");
        LblFiles.Text = T("ФАЙЛОВ", "FILES", "文件");
        LblSystem.Text = T("СИСТЕМА", "SYSTEM", "系统");
        _vm.ErrorText = "";
        _vm.StatusLine = ModeWord();

        switch (_vm.Screen)
        {
            case SetupVm.Phase.Welcome when _vm.ScreenMode == SetupVm.Mode.Install:
                _vm.StatusLine = ModeWord();
                _vm.HeadlineText = T("Пара секунд — и ты в сети", "A few seconds and you're in", "几秒钟，即刻连网");
                _vm.Subline = T("Установка займёт меньше минуты. Аккаунт создаётся при первом входе в приложение.",
                    "Installation takes under a minute. Your account is created on first launch.",
                    "安装不到一分钟。首次启动应用时会自动创建账号。");
                Giant.Stroke = (Brush)FindResource("OutlineVariantBrush");
                Giant.Fill = null;
                break;

            case SetupVm.Phase.Welcome: // подтверждение удаления
                _vm.StatusLine = ModeWord();
                _vm.HeadlineText = T("Удалить Godji VPN?", "Uninstall Godji VPN?", "卸载 Godji VPN？");
                _vm.Subline = T(
                    "Остановим приложение и удалим файлы. Настройки в {0} не тронем — пригодятся при переустановке.",
                    "We'll stop the app and remove its files. Your settings in {0} stay — handy if you reinstall.",
                    "将关闭应用并删除文件。{0} 中的设置会保留，重装时可以继续使用。")
                    .Replace("{0}", UninstallEngine.UserDataDir);
                Giant.Stroke = (Brush)FindResource("OutlineVariantBrush");
                Giant.Fill = null;
                break;

            case SetupVm.Phase.Running:
                Giant.Stroke = (Brush)FindResource("OutlineVariantBrush");
                _vm.StatusLine = $"{ModeWord()} · 0%";
                _vm.HeadlineText = _vm.ScreenMode == SetupVm.Mode.Install
                    ? T("Устанавливаем…", "Setting things up…", "正在安装…")
                    : T("Удаляем…", "Removing…", "正在卸载…");
                _vm.Subline = "";
                break;

            case SetupVm.Phase.Done when _vm.ScreenMode == SetupVm.Mode.Install:
                _vm.StatusLine = $"{ModeWord()} · 100%";
                _vm.HeadlineText = T("Готово к работе", "Ready to go", "一切就绪");
                _vm.Subline = T("Ярлык — в меню «Пуск». До встречи внутри.",
                    "Shortcut is in the Start menu. See you inside.", "快捷方式在开始菜单中。应用内见。");
                Giant.Stroke = (Brush)FindResource("OutlineVariantBrush");
                Giant.Fill = (Brush)FindResource("PrimaryBrush");
                _vm.Percent = 100;
                break;

            case SetupVm.Phase.Done: // удаление завершено
                _vm.StatusLine = $"{ModeWord()} · 100%";
                _vm.HeadlineText = T("Удалено", "Removed", "已卸载");
                _vm.Subline = _vm.DeleteUserData
                    ? T("Настройки тоже удалены.", "Your settings were removed too.", "设置也已删除。")
                    : T("Настройки остались в {0}.", "Your settings remain in {0}.", "设置保留在 {0}。")
                        .Replace("{0}", UninstallEngine.UserDataDir);
                Giant.Stroke = (Brush)FindResource("OutlineVariantBrush");
                Giant.Fill = null;
                _vm.Percent = 100;
                break;

            case SetupVm.Phase.Cancelled:
                _vm.StatusLine = ModeWord();
                _vm.HeadlineText = _vm.ScreenMode == SetupVm.Mode.Install
                    ? T("Установка отменена", "Setup cancelled", "已取消安装")
                    : T("Удаление отменено", "Uninstall cancelled", "已取消卸载");
                _vm.Subline = T("Ничего не сломалось — можно начать заново.",
                    "Nothing broke — you can start over.", "没有任何问题，可以重新开始。");
                Giant.Stroke = (Brush)FindResource("OutlineVariantBrush");
                Giant.Fill = null;
                break;

            case SetupVm.Phase.Error:
                // StatusLine/Headline выставляет ShowError, текст ошибки — в _vm.ErrorText.
                break;
        }

        RebuildButtons();
    }

    public void ShowError(string message)
    {
        _vm.Screen = SetupVm.Phase.Error;
        _vm.StatusLine = ModeWord();
        _vm.HeadlineText = T("Не получилось", "Something went wrong", "出了点问题");
        _vm.Subline = "";
        _vm.ErrorText = message;
        Giant.Stroke = (Brush)FindResource("TerracottaBrush");
        Giant.Fill = null;
        RebuildButtons();
    }

    // ── гигантские буквы: заливка-полотно по прогрессу ────────────────────

    private void UpdateGiant(double percent)
    {
        double t = Math.Clamp(percent / 100.0, 0, 1);
        var primary = ((SolidColorBrush)FindResource("PrimaryBrush")).Color;
        var clear = Colors.Transparent;

        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
            GradientStops =
            {
                _vm.ScreenMode == SetupVm.Mode.Install
                    ? new GradientStop(clear, 0)
                    : new GradientStop(primary, 0),
                _vm.ScreenMode == SetupVm.Mode.Install
                    ? new GradientStop(clear, t)
                    : new GradientStop(primary, t),
                _vm.ScreenMode == SetupVm.Mode.Install
                    ? new GradientStop(primary, t)
                    : new GradientStop(clear, t),
                _vm.ScreenMode == SetupVm.Mode.Install
                    ? new GradientStop(primary, 1)
                    : new GradientStop(clear, 1)
            }
        };
        brush.Freeze();
        Giant.Fill = t <= 0.001 ? null : brush;
    }

    private void OnStep(EngineStep step)
    {
        _vm.Percent = step.Percent;
        _vm.StatusLine = $"{PhaseText(step.PhaseKey)} · {(int)step.Percent}%";
        if (step.Files != null) _vm.FilesValue = step.Files;
        UpdateGiant(step.Percent);
    }

    // ── кнопки ────────────────────────────────────────────────────────────

    private void RebuildButtons()
    {
        ButtonsHost.Children.Clear();
        Button Add(string text, Style style, RoutedEventHandler onClick)
        {
            var b = new Button { Content = text, Style = style, Margin = new Thickness(0, 0, 14, 0) };
            b.Click += onClick;
            ButtonsHost.Children.Add(b);
            return b;
        }
        var flat = (Style)FindResource("FlatCta");
        var ghost = (Style)FindResource("GhostCta");
        var danger = (Style)FindResource("DangerCta");

        switch (_vm.Screen)
        {
            case SetupVm.Phase.Welcome when _vm.ScreenMode == SetupVm.Mode.Install:
                Add(T("УСТАНОВИТЬ", "INSTALL", "安装"), flat, (_, _) => StartInstall());
                break;
            case SetupVm.Phase.Welcome:
                Add(T("УДАЛИТЬ", "UNINSTALL", "卸载"), danger, (_, _) => StartUninstall());
                Add(T("ЗАКРЫТЬ", "CLOSE", "关闭"), ghost, (_, _) => Close());
                break;
            case SetupVm.Phase.Running:
                Add(T("ОТМЕНИТЬ", "CANCEL", "取消"), ghost, (_, _) => Cancel());
                break;
            case SetupVm.Phase.Done when _vm.ScreenMode == SetupVm.Mode.Install:
                Add(T("ЗАПУСТИТЬ", "LAUNCH", "启动"), flat, (_, _) => { InstallerEngine.Launch(_installDir); Close(); });
                break;
            case SetupVm.Phase.Done:
                Add(T("ЗАКРЫТЬ", "CLOSE", "关闭"), flat, (_, _) => Close());
                break;
            case SetupVm.Phase.Cancelled:
            case SetupVm.Phase.Error:
                Add(_vm.ScreenMode == SetupVm.Mode.Install
                        ? T("УСТАНОВИТЬ", "INSTALL", "安装")
                        : T("УДАЛИТЬ", "UNINSTALL", "卸载"),
                    flat, (_, _) =>
                    {
                        if (_vm.ScreenMode == SetupVm.Mode.Install) StartInstall();
                        else StartUninstall();
                    });
                Add(T("ЗАКРЫТЬ", "CLOSE", "关闭"), ghost, (_, _) => Close());
                break;
        }
    }

    // ── запуск работы ─────────────────────────────────────────────────────

    private async void StartInstall()
    {
        App.LogRun("install: start (dryRun=" + _dryRun + ", dir=" + _installDir + ")");
        _vm.Screen = SetupVm.Phase.Running;
        _vm.Percent = 0;
        UpdateGiant(0);
        ApplyState();
        _cts = new CancellationTokenSource();
        var progress = new Progress<EngineStep>(OnStep);
        try
        {
            await Task.Run(() => _installer.RunAsync(_installDir, _vm, _dryRun, progress, _cts.Token));
            if (_vm.LaunchAfter && !_dryRun) // в dry-run запускать нечего — показываем финал
            {
                InstallerEngine.Launch(_installDir);
                Close();
                return;
            }
            _vm.Screen = SetupVm.Phase.Done;
            ApplyState();
        }
        catch (OperationCanceledException)
        {
            App.LogRun("install: cancelled");
            _vm.Screen = SetupVm.Phase.Cancelled;
            ApplyState();
        }
        catch (Exception ex)
        {
            App.LogRun("install FAILED: " + ex);
            ShowError(ex.Message);
        }
    }

    private async void StartUninstall()
    {
        App.LogRun("uninstall: start (dryRun=" + _dryRun + ")");
        _vm.Screen = SetupVm.Phase.Running;
        _vm.Percent = 100; // при удалении заливка «съедается» слева направо
        UpdateGiant(0);
        ApplyState();
        _cts = new CancellationTokenSource();
        var progress = new Progress<EngineStep>(OnStep);
        try
        {
            await Task.Run(() => _uninstaller.RunAsync(_vm, _dryRun, progress, _cts.Token));
            _vm.Screen = SetupVm.Phase.Done;
            ApplyState();
        }
        catch (OperationCanceledException)
        {
            App.LogRun("uninstall: cancelled");
            _vm.Screen = SetupVm.Phase.Cancelled;
            ApplyState();
        }
        catch (Exception ex)
        {
            App.LogRun("uninstall FAILED: " + ex);
            ShowError(ex.Message);
        }
    }

    private void Cancel() => _cts?.Cancel();

    /// Тихий деинсталляция без окна (UninstallString с /quiet из реестра).
    public async Task RunQuietUninstallAsync()
    {
        var vm = new SetupVm { DeleteUserData = false };
        await _uninstaller.RunAsync(vm, _dryRun,
            new Progress<EngineStep>(_ => { }), CancellationToken.None);
    }

    /// Обработчик DispatcherUnhandledException: ошибка в любой момент — экран ошибки вместо падения.
    public void ReportCrash(Exception ex) => ShowError(ex.Message);

    // ── рамка окна ────────────────────────────────────────────────────────

    private void Titlebar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Screen == SetupVm.Phase.Running) Cancel();
        else Close();
    }
}
