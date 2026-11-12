using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GodjiVpn.Views;

public partial class PlansView : UserControl
{
    public PlansView()
    {
        InitializeComponent();
    }

    private void DeviceMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } b) return;
        menu.PlacementTarget = b;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }
}
