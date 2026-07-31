using Avalonia.Controls;
using Avalonia.Interactivity;
using Redture.App.ViewModels;

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

    /// <summary>
    /// Copies the registry command to the clipboard.
    /// </summary>
    /// <remarks>
    /// The one piece of logic in this file, and it is here rather than in the
    /// view model because the clipboard belongs to the window: reaching it
    /// means walking up to the <see cref="TopLevel"/>, which a view model has
    /// no business knowing about.
    /// </remarks>
    private async void OnCopyGammaCommand(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ControlPanelViewModel viewModel)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(viewModel.GammaRangeCommand);
        }
    }
}
