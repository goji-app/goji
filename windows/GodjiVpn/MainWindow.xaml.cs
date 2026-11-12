using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using GodjiVpn.Services;

namespace GodjiVpn;

public partial class MainWindow : Window
{
    // DWM-атрибуты Windows 11: тёмный режим рамки, цвет заголовка и цвет текста заголовка.
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmBorderColor = 34;
    private const int DwmCaptionColor = 35;
    private const int DwmTextColor = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private const int HotkeyId = 0x6A01, WmHotkey = 0x0312, ModCtrlShiftNoRepeat = 0x0002 | 0x0004 | 0x4000, VkG = 0x47;

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, int modifiers, int vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    /// <summary>Нажата глобальная горячая клавиша Ctrl+Shift+G (подключить / отключить VPN).</summary>
    public Action? HotkeyPressed { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        Utils.MemoryTrim.Attach(this);
        SourceInitialized += (_, _) =>
        {
            ApplyTitleBar();
            var src = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            // Если сочетание занято другой программой, RegisterHotKey вернёт false: остаёмся без него.
            if (src != null && RegisterHotKey(src.Handle, HotkeyId, ModCtrlShiftNoRepeat, VkG))
                src.AddHook((IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) =>
                {
                    if (msg == WmHotkey && w.ToInt32() == HotkeyId) { handled = true; HotkeyPressed?.Invoke(); }
                    return IntPtr.Zero;
                });
            if (ThemeService.Current is { } theme) theme.Changed += () => Dispatcher.Invoke(ApplyTitleBar);
        };
    }

    /// <summary>Верхняя строка окна рисуется системой: красим её в фон приложения и подбираем цвет текста,
    /// иначе при тёмной теме приложения она остаётся светлой. На Windows 10 цветовые атрибуты игнорируются.</summary>
    private void ApplyTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var dark = ThemeService.Current?.IsDark == true;
        var dm = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref dm, sizeof(int));
        if (TryFindResource("BgSolidBrush") is SolidColorBrush bg)
        {
            var caption = ToColorRef(bg.Color);
            DwmSetWindowAttribute(hwnd, DwmCaptionColor, ref caption, sizeof(int));
            DwmSetWindowAttribute(hwnd, DwmBorderColor, ref caption, sizeof(int));
        }
        if (TryFindResource("TextPrimaryBrush") is SolidColorBrush ink)
        {
            var text = ToColorRef(ink.Color);
            DwmSetWindowAttribute(hwnd, DwmTextColor, ref text, sizeof(int));
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero) UnregisterHotKey(hwnd, HotkeyId);
        base.OnClosed(e);
    }

    private static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);
}
