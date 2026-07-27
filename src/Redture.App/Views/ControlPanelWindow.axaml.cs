using Avalonia.Controls;

namespace Redture.App.Views;

/// <summary>
/// The control panel. Deliberately code-behind-free: everything it shows comes
/// from <see cref="ViewModels.ControlPanelViewModel"/> through compiled
/// bindings, and its lifetime is managed by
/// <see cref="Services.ControlPanelPresenter"/>.
/// </summary>
public sealed partial class ControlPanelWindow : Window
{
    public ControlPanelWindow() => InitializeComponent();
}
