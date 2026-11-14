using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GodjiSetup;

/// <summary>Экран установщика: три состояния установки (приветствие / работа / финал-ошибка)
/// и три состояния удаления (подтверждение / работа / финал). Окно читает свойства видимости
/// и тексты напрямую; тяжёлой логики в VM нет — двигатели (InstallerEngine/UninstallEngine)
/// живут отдельно.</summary>
public sealed class SetupVm : INotifyPropertyChanged
{
    public enum Mode { Install, Uninstall }
    public enum Phase { Welcome, Confirm, Running, Done, Cancelled, Error }

    private Mode _mode;
    private Phase _phase;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Mode ScreenMode
    {
        get => _mode;
        set { if (_mode != value) { _mode = value; FireAll(); } }
    }

    public Phase Screen
    {
        get => _phase;
        set { if (_phase != value) { _phase = value; FireAll(); } }
    }

    // ── видимости экранов ──────────────────────────────────────────────────
    public bool IsWelcome => ScreenMode == Mode.Install && Screen == Phase.Welcome;
    public bool IsConfirm => ScreenMode == Mode.Uninstall && Screen == Phase.Welcome;
    public bool IsRunning => Screen == Phase.Running;
    public bool IsDone => Screen == Phase.Done;
    public bool IsCancelled => Screen == Phase.Cancelled;
    public bool IsError => Screen == Phase.Error;
    public bool ShowErrorBlock => IsError || IsCancelled;
    public bool IsInstallMode => ScreenMode == Mode.Install;

    // ── динамические тексты/числа ─────────────────────────────────────────
    private string _statusLine = "";
    private string _headlineText = "";
    private string _subline = "";
    private string _errorText = "";
    private string _detail = "";

    public string StatusLine { get => _statusLine; set => Set(ref _statusLine, value); }
    public string HeadlineText { get => _headlineText; set => Set(ref _headlineText, value); }
    public string Subline { get => _subline; set => Set(ref _subline, value); }
    public string ErrorText { get => _errorText; set => Set(ref _errorText, value); }
    public string Detail { get => _detail; set => Set(ref _detail, value); }

    private double _percent;          // 0..100 — заливка гигантских букв и полоса прогресса
    private string _percentText = "";
    private string _filesValue = "";

    public double Percent
    {
        get => _percent;
        set
        {
            if (Set(ref _percent, value))
            {
                PercentText = $"{(int)Math.Round(value)}%";
                RaisePropertyChanged(nameof(ProgressScaleX));
            }
        }
    }
    public string PercentText { get => _percentText; private set => Set(ref _percentText, value); }
    public double ProgressScaleX => Math.Max(0.001, _percent / 100.0);
    public string FilesValue { get => _filesValue; set => Set(ref _filesValue, value); }

    // ── статичные тексты и значения таблицы (заполняет окно при смене языка/состояния) ──
    private string _desktopCheckText = "";
    private string _launchCheckText = "";
    private string _userDataCheckText = "";
    private string _versionValue = "";
    private string _pathValue = "";
    private string _sizeValue = "";
    private string _systemValue = "";

    public string DesktopCheckText { get => _desktopCheckText; set => Set(ref _desktopCheckText, value); }
    public string LaunchCheckText { get => _launchCheckText; set => Set(ref _launchCheckText, value); }
    public string UserDataCheckText { get => _userDataCheckText; set => Set(ref _userDataCheckText, value); }
    public string VersionValue { get => _versionValue; set => Set(ref _versionValue, value); }
    public string PathValue { get => _pathValue; set => Set(ref _pathValue, value); }
    public string SizeValue { get => _sizeValue; set => Set(ref _sizeValue, value); }
    public string SystemValue { get => _systemValue; set => Set(ref _systemValue, value); }

    // ── настройки установки ───────────────────────────────────────────────
    private bool _desktopShortcut = true;
    private bool _launchAfter = true;
    private bool _deleteUserData;

    public bool DesktopShortcut { get => _desktopShortcut; set => Set(ref _desktopShortcut, value); }
    public bool LaunchAfter { get => _launchAfter; set => Set(ref _launchAfter, value); }
    public bool DeleteUserData { get => _deleteUserData; set => Set(ref _deleteUserData, value); }

    // ── бухгалтерия ───────────────────────────────────────────────────────
    public void FireAll()
    {
        foreach (var p in new[]
                 { nameof(IsWelcome), nameof(IsConfirm), nameof(IsRunning), nameof(IsDone),
                   nameof(IsCancelled), nameof(IsError), nameof(ShowErrorBlock), nameof(IsInstallMode) })
            RaisePropertyChanged(p);
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        RaisePropertyChanged(name);
        return true;
    }

    private void RaisePropertyChanged(string? name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
