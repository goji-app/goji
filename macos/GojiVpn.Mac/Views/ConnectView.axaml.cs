using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using GodjiVpn.Services;
using GodjiVpn.ViewModels;

namespace GodjiVpn.Views;

public partial class ConnectView : UserControl
{
    public ConnectView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ConnectViewModel vm) vm.PropertyChanged += OnVmPropertyChanged;
            ApplyState();
        };
        AttachedToVisualTree += (_, _) =>
        {
            if (ThemeService.Current is { } theme) theme.Changed += ApplyState;
            ApplyState();
        };
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ConnectViewModel.IsConnected) or nameof(ConnectViewModel.IsConnecting)) ApplyState();
    }

    private void OnCodePressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ConnectViewModel vm) vm.NextServerCommand.Execute(null);
    }

    private Color Res(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var v) && v is SolidColorBrush b ? b.Color : Colors.Magenta;

    /// <summary>Подбирает цвета по состоянию (выкл / подключение / в сети); Border плавно перекрашивается сам (BrushTransition).</summary>
    private void ApplyState()
    {
        if (DataContext is not ConnectViewModel vm) return;
        var state = vm.IsConnected ? 2 : vm.IsConnecting ? 1 : 0;
        var fill = state switch { 2 => Res("HomeOnFillBrush"), 1 => Res("HomeBusyFillBrush"), _ => Res("SurfaceBaseBrush") };
        var ink = state switch { 2 => Res("HomeOnInkBrush"), 1 => Res("HomeBusyInkBrush"), _ => Res("TextPrimaryBrush") };
        var mut = state == 0 ? Res("TextSecondaryBrush") : Color.FromArgb(200, ink.R, ink.G, ink.B);
        var btn = state == 1 ? Res("HomeBusyBtnBrush") : Res("InkBtnBrush");
        Resources["HomeFillBrush"] = new SolidColorBrush(fill);
        Resources["HomeInkBrush"] = new SolidColorBrush(ink);
        Resources["HomeMutBrush"] = new SolidColorBrush(mut);
        Resources["HomeBtnBrush"] = new SolidColorBrush(btn);
    }
}
