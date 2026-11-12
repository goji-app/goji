using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GodjiVpn.Services;
using GodjiVpn.ViewModels;

namespace GodjiVpn.Views;

public partial class ConnectView : UserControl
{
    private static readonly Duration Fade = new(TimeSpan.FromMilliseconds(500));

    public ConnectView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += (_, _) => ApplyState(animate: true);
        Loaded += (_, _) =>
        {
            if (ThemeService.Current is { } theme) theme.Changed += () => Dispatcher.Invoke(() => ApplyState(animate: false));
            ApplyState(animate: false);
        };
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ConnectViewModel oldVm) oldVm.PropertyChanged -= OnVmPropertyChanged;
        if (e.NewValue is ConnectViewModel newVm) newVm.PropertyChanged += OnVmPropertyChanged;
        ApplyState(animate: false);
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ConnectViewModel.IsConnected) or nameof(ConnectViewModel.IsConnecting))
            ApplyState(animate: true);
    }

    private static Color Res(string key) =>
        Application.Current.TryFindResource(key) is SolidColorBrush b ? b.Color : Colors.Magenta;

    /// <summary>Подбирает цвета по состоянию (выкл / подключение / в сети) и плавно перекрашивает кисти;
    /// заливка дублируется в NavFillBrush — ей же красятся окно и нижняя панель. Вне вкладки «Главная»
    /// NavFill возвращается к обычному фону — переход между вкладками без резкого скачка.</summary>
    private void ApplyState(bool animate)
    {
        if (DataContext is not ConnectViewModel vm) return;
        var state = vm.IsConnected ? 2 : vm.IsConnecting ? 1 : 0;
        var sage = ThemeService.Current?.PaletteId == "sage";
        var fill = state switch { 2 => Res("HomeOnFillBrush"), 1 => Res("HomeBusyFillBrush"), _ => Res("SurfaceBaseBrush") };
        var ink = state switch { 2 => Res("HomeOnInkBrush"), 1 => Res("HomeBusyInkBrush"), _ => Res("TextPrimaryBrush") };
        var mut = state == 0 ? Res("TextSecondaryBrush") : Color.FromArgb(200, ink.R, ink.G, ink.B);
        var btn = state == 1 ? Res("HomeBusyBtnBrush") : Res("InkBtnBrush");

        Set("HomeFillBrush", fill, animate); Set("HomeInkBrush", ink, animate); Set("HomeMutBrush", mut, animate);
        Set("HomeBtnBrush", btn, animate);
    }

    private void Set(string key, Color to, bool animate) => Animate((SolidColorBrush)Resources[key], to, animate);

    private static void Animate(SolidColorBrush brush, Color to, bool animate)
    {
        if (brush.IsFrozen) return;
        if (!animate) { brush.BeginAnimation(SolidColorBrush.ColorProperty, null); brush.Color = to; return; }
        brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to, Fade) { EasingFunction = new QuadraticEase() });
    }
}
