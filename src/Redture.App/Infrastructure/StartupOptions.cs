namespace Redture.App.Infrastructure;

/// <summary>
/// Command-line switches understood at launch.
/// </summary>
/// <param name="ShowPanelOnStart">
/// Open the control panel immediately instead of starting silently in the tray.
/// The distinction matters for the auto-start feature: launching at logon must
/// stay out of the user's way, while a manual launch should show something.
/// </param>
internal sealed record StartupOptions(bool ShowPanelOnStart)
{
    private const string ShowSwitch = "--show";

    public static StartupOptions Parse(string[] args) =>
        new(ShowPanelOnStart: args.Any(arg => string.Equals(arg, ShowSwitch, StringComparison.OrdinalIgnoreCase)));
}
