using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using GodjiVpn.ViewModels;
using Hardcodet.Wpf.TaskbarNotification;

namespace GodjiVpn.Services;

/// <summary>Иконка в трее со статусом — Hardcodet.NotifyIcon.Wpf вместо
/// System.Windows.Forms.NotifyIcon: не тянет UseWindowsForms=true в csproj, который
/// конфликтует с System.Windows.Application/UserControl (см. комментарий в csproj из первого
/// прохода). Аналог кастомного двухстрочного уведомления Android (notification_status.xml) —
/// здесь это просто текст тултипа "страна/статус", т.к. Windows не ограничивает layout трея
/// так же, как Android RemoteViews.</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly VpnEngine _vpnEngine;
    private readonly MenuItem _toggleItem;
    private readonly MenuItem _serversItem;
    private readonly ConnectViewModel _connect;
    private readonly SubscriptionRepository _subscription;
    // Хранятся, чтобы снять в Dispose — иначе движок/вьюмодель продолжали бы держать живой
    // TrayIconService после Dispose (утечка + апдейты мёртвой иконки).
    private readonly PropertyChangedEventHandler _onEngineChanged;
    private readonly PropertyChangedEventHandler _onConnectChanged;

    // Значок показывает состояние без открытия окна: серый — отключено, жёлтая точка —
    // подключаемся, зелёная — подключено (Assets/Images/tray-*.png, из logo.png).
    private static readonly BitmapImage IconOff = TrayImage("tray-off.png");
    private static readonly BitmapImage IconConnecting = TrayImage("tray-connecting.png");
    private static readonly BitmapImage IconOn = TrayImage("tray-on.png");

    private static BitmapImage TrayImage(string name) => new(new Uri($"pack://application:,,,/Assets/Images/{name}"));

    public bool IsExiting { get; private set; }

    public TrayIconService(VpnEngine vpnEngine, ConnectViewModel connect, SubscriptionRepository subscription, Window window, Action exit)
    {
        _vpnEngine = vpnEngine;
        _connect = connect;
        _subscription = subscription;

        _icon = new TaskbarIcon
        {
            IconSource = IconOff,
            ToolTipText = "Goji VPN — отключено"
        };

        var openItem = new MenuItem { Header = "Открыть" };
        openItem.Click += (_, _) => ShowWindow(window);

        _toggleItem = new MenuItem { Header = "Подключить" };
        _toggleItem.Click += async (_, _) =>
        {
            if (connect.ToggleCommand.CanExecute(null)) await connect.ToggleCommand.ExecuteAsync(null);
        };

        // Смена узла без открытия окна: подменю пересобирается при каждом открытии меню —
        // список и выбранный узел всегда актуальны, и пинги пересчитывать не нужно.
        _serversItem = new MenuItem { Header = "Сменить сервер" };
        _serversItem.SubmenuOpened += (_, _) => RebuildServersMenu();

        var exitItem = new MenuItem { Header = "Выход" };
        exitItem.Click += (_, _) => { IsExiting = true; exit(); };

        var menu = new ContextMenu();
        menu.Items.Add(openItem);
        menu.Items.Add(_toggleItem);
        menu.Items.Add(_serversItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);
        // Меню создаётся вне визуального дерева окна: без явных стилей фон рисует дефолтный
        // WPF-шаблон (светлый SystemColors), а текст уходит под глобальный неявный стиль
        // TextBlock из App.xaml (в тёмной теме — почти белый) → нечитаемо (скриншот 2026-11-03).
        // Styles описаны в App.xaml (TrayContextMenu/TrayMenuItem), тема подхватывается через
        // DynamicResource и переживает смену светлой/тёмной в рантайме.
        menu.Style = (Style)Application.Current.TryFindResource("TrayContextMenu");
        openItem.Style = (Style)Application.Current.TryFindResource("TrayMenuItem");
        _toggleItem.Style = openItem.Style;
        _serversItem.Style = openItem.Style;
        exitItem.Style = openItem.Style;
        _icon.ContextMenu = menu;

        _icon.TrayMouseDoubleClick += (_, _) => ShowWindow(window);

        _onEngineChanged = (_, _) => UpdateStatus();
        _vpnEngine.PropertyChanged += _onEngineChanged;
        // Название узла в меню меняется вместе с выбором на вкладке "Серверы".
        _onConnectChanged = (_, e) =>
        {
            if (e.PropertyName == nameof(ConnectViewModel.CurrentNodeName)) UpdateStatus();
        };
        connect.PropertyChanged += _onConnectChanged;
        UpdateStatus();
    }

    /// <summary>Выбранный узел помечен; порядок — как на вкладке "Серверы" (сортировка
    /// пользователя). Клик выбирает узел и переподключает туннель, если он поднят;
    /// выключенный VPN при следующем подключении возьмёт уже новый узел.</summary>
    private void RebuildServersMenu()
    {
        var itemStyle = (Style)Application.Current.TryFindResource("TrayMenuItem");
        _serversItem.Items.Clear();
        var nodes = _subscription.Nodes;
        if (nodes.Count == 0)
        {
            var empty = new MenuItem { Header = "Список пуст — обновите подписку", IsEnabled = false };
            empty.Style = itemStyle;
            _serversItem.Items.Add(empty);
            return;
        }
        // Больше ~25 пунктов в подменю трея не прокручивается на маленьких экранах —
        // показываем верх списка; полный список всегда на вкладке "Серверы".
        foreach (var node in nodes.Take(25))
        {
            var id = node.Id;
            var selected = id == _subscription.SelectedId;
            var item = new MenuItem
            {
                Header = (selected ? "●  " : "") + node.Name,
                IsChecked = selected,
                IsEnabled = !selected || _vpnEngine.IsRunning
            };
            item.Style = itemStyle;
            item.Click += async (_, _) =>
            {
                _subscription.Select(id);
                if (_vpnEngine.IsRunning)
                    await _vpnEngine.ReconnectAsync();
                else if (!_vpnEngine.IsConnecting && _subscription.SelectedNode is { } picked)
                    await _vpnEngine.ConnectAsync(picked);
            };
            _serversItem.Items.Add(item);
        }
    }

    private static void ShowWindow(Window window)
    {
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private void UpdateStatus()
    {
        // VpnEngine поднимает PropertyChanged из фонового потока (ConnectAsync/DisconnectAsync
        // идут через ConfigureAwait(false)) — трей-иконка и её меню, как любые DependencyObject,
        // требуют UI-потока.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess()) { dispatcher.BeginInvoke(UpdateStatus); return; }

        // Аналог ярлыка "Подключиться: <последний узел>" на иконке Android (59e214b): из трея
        // VPN поднимается на выбранном узле без открытия окна — и видно, на каком именно.
        var node = _connect.CurrentNodeName;
        var icon = _vpnEngine.IsRunning ? IconOn : _vpnEngine.IsConnecting ? IconConnecting : IconOff;
        if (!ReferenceEquals(_icon.IconSource, icon)) _icon.IconSource = icon;
        _icon.ToolTipText = _vpnEngine.IsRunning ? $"Goji VPN — подключено: {node}"
            : _vpnEngine.IsConnecting ? $"Goji VPN — подключаемся: {node}"
            : "Goji VPN — отключено";
        _toggleItem.Header = _vpnEngine.IsRunning ? "Отключить VPN"
            : _vpnEngine.IsConnecting ? $"Подключаемся: {node}"
            : $"Подключиться: {node}";
        _toggleItem.IsEnabled = !_vpnEngine.IsConnecting;
    }

    /// <summary>Аналог NotificationCompat-уведомлений Android (см. SubscriptionNotifier) —
    /// здесь просто balloon tip на самой трей-иконке, простейший системный эквивалент toast
    /// на Windows без отдельной инфраструктуры Action Center/COM-регистрации.</summary>
    public void ShowBalloon(string title, string text)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess()) { dispatcher.BeginInvoke(() => ShowBalloon(title, text)); return; }
        _icon.ShowBalloonTip(title, text, BalloonIcon.Info);
    }

    public void Dispose()
    {
        _vpnEngine.PropertyChanged -= _onEngineChanged;
        _connect.PropertyChanged -= _onConnectChanged;
        _icon.Dispose();
    }
}
