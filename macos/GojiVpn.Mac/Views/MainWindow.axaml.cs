using Avalonia.Controls;

namespace GodjiVpn.Views;

public partial class MainWindow : Window
{
    /// <summary>Command+Shift+G: подключить / отключить VPN.</summary>
    public Action? HotkeyPressed { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        Utils.MemoryTrim.Attach(this);
        KeyDown += (_, e) =>
        {
            // shortcut: только пока окно в фокусе, глобальная клавиша на macOS требует нативного API.
            if (e.Key == Avalonia.Input.Key.G && e.KeyModifiers == (Avalonia.Input.KeyModifiers.Meta | Avalonia.Input.KeyModifiers.Shift))
            { e.Handled = true; HotkeyPressed?.Invoke(); }
        };
    }
}
