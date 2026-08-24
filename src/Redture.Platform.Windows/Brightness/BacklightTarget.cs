using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Redture.Platform.Abstractions.Brightness;

namespace Redture.Platform.Windows.Brightness;

/// <summary>
/// One display whose backlight can be driven, regardless of how.
/// </summary>
/// <remarks>
/// The two mechanisms (DDC/CI over the cable, WMI for a laptop panel) differ
/// only in how a level is written and what range it uses, so everything else —
/// percentage conversion, redundant-write suppression, error handling — lives
/// here once.
/// </remarks>
[SupportedOSPlatform("windows")]
internal abstract class BacklightTarget : IDisposable
{
    /// <summary>
    /// Last level written, or -1 when nothing has been written yet. Guards the
    /// one rule that matters most for a device this slow: never send a value
    /// the monitor already has.
    /// </summary>
    private int _lastWrittenLevel = -1;

    protected BacklightTarget(string displayId, string name, BrightnessMechanism mechanism, double initialPercent)
    {
        DisplayId = displayId;
        Name = name;
        Mechanism = mechanism;
        InitialPercent = initialPercent;
    }

    public string DisplayId { get; }

    public string Name { get; }

    public BrightnessMechanism Mechanism { get; }

    /// <summary>Level the display had when Redture took over, 0–100.</summary>
    public double InitialPercent { get; private set; }

    /// <summary>
    /// Replaces the level read at discovery with one remembered from an
    /// earlier discovery of the same display.
    /// </summary>
    /// <remarks>
    /// Discovery re-runs on every display change, and by then the monitor is
    /// showing Redture's dimming rather than the user's own level. Without
    /// this, that dimmed value is adopted as the one to hand back, and each
    /// change ratchets it further down until giving the display back does
    /// nothing at all.
    /// </remarks>
    public void AdoptRememberedInitial(double percent) => InitialPercent = percent;

    public HardwareBrightnessTarget ToDescriptor() => new(DisplayId, Name, Mechanism, InitialPercent);

    /// <summary>
    /// Writes a percentage, skipping the call entirely when it maps to the
    /// level already on the display.
    /// </summary>
    public void ApplyPercent(double percent, ILogger logger)
    {
        int level = ToDeviceLevel(Math.Clamp(percent, 0d, 100d));
        if (level == _lastWrittenLevel)
        {
            return;
        }

        try
        {
            if (TryWriteLevel(level))
            {
                _lastWrittenLevel = level;
                logger.LogDebug("Backlight for {Display} set to level {Level} ({Percent:0}%).", Name, level, percent);
                return;
            }

            logger.LogWarning("Backlight write to {Display} failed at level {Level}.", Name, level);
        }
        catch (Exception ex)
        {
            // A monitor that stops answering mid-session must not take the app
            // down with it; the next write will simply try again.
            logger.LogError(ex, "Backlight write to {Display} threw.", Name);
        }
    }

    /// <summary>
    /// Forgets the cached level so the next write always reaches the display.
    /// Needed before restoring, where the value being written may be the one we
    /// believe is already set.
    /// </summary>
    public void ForgetLastWrite() => _lastWrittenLevel = -1;

    /// <summary>Converts a 0–100 percentage into this device's own scale.</summary>
    protected abstract int ToDeviceLevel(double percent);

    /// <summary>Writes a device-scale level. Returns false on a clean failure.</summary>
    protected abstract bool TryWriteLevel(int level);

    public virtual void Dispose()
    {
    }
}
