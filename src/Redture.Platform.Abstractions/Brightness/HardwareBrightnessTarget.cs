namespace Redture.Platform.Abstractions.Brightness;

/// <summary>
/// A display whose backlight Redture can drive.
/// </summary>
/// <param name="DisplayId">
/// Adapter output id, matching <c>DisplayInfo.Id</c> where one could be
/// resolved. Empty for mechanisms that are not tied to a specific output.
/// </param>
/// <param name="Name">Monitor name, for display in the UI.</param>
/// <param name="Mechanism">How this target is driven.</param>
/// <param name="InitialPercent">
/// Backlight level found when Redture took over, 0–100. Restored on exit so
/// quitting the app never leaves a monitor at a level the user did not choose.
/// </param>
public sealed record HardwareBrightnessTarget(
    string DisplayId,
    string Name,
    BrightnessMechanism Mechanism,
    double InitialPercent);
