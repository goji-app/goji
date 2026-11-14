using System.Runtime.InteropServices;

namespace GodjiSetup;

/// <summary>Скругление углов безрамочного окна (Win11 DWM; на Win10 вернётся ошибка —
/// останутся прямые углы, это штатный фолбэк).</summary>
internal static class Win32
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    public static void TryRoundCorners(System.Windows.Window w)
    {
        try
        {
            var pref = DWMWCP_ROUND;
            DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(w).EnsureHandle(),
                DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
        }
        catch
        {
            // Не Win11 — прямые углы.
        }
    }
}
