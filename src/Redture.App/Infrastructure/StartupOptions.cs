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

    /// <summary>
    /// Switch used by the short-lived elevated process that lifts Windows'
    /// gamma range restriction. Declared here rather than referenced from the
    /// Windows backend so the check itself stays platform-neutral.
    /// </summary>
    private const string UnlockGammaRangeSwitch = "--unlock-gamma-range";

    public static StartupOptions Parse(string[] args) =>
        new(ShowPanelOnStart: Has(args, ShowSwitch));

    /// <summary>
    /// Whether this process was started solely to apply the registry change.
    /// Checked before anything else, including the single-instance guard.
    /// </summary>
    public static bool IsGammaRangeUnlockRequest(string[] args) => Has(args, UnlockGammaRangeSwitch);

    private static bool Has(string[] args, string name) =>
        args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
}
