using System.Text.Json.Serialization;

namespace Redture.Core.Settings;

/// <summary>
/// Everything Redture persists between runs. Plain mutable POCO on purpose:
/// it is bound directly by the view models and serialised as-is to JSON.
/// </summary>
/// <remarks>
/// Schema evolution strategy: new properties may be added freely — the JSON
/// deserialiser leaves missing members at their default value, so an older
/// settings file simply picks up the new defaults. <see cref="SchemaVersion"/>
/// exists for the case where a property changes *meaning* (e.g. a range or unit
/// change), which needs an explicit migration step before that can happen.
/// </remarks>
public sealed class AppSettings
{
    /// <summary>Schema version written by this build.</summary>
    public const int CurrentSchemaVersion = 1;

    // --- Limits ------------------------------------------------------------
    // Centralised here so the view models, the validator and the tests all
    // agree on the same numbers.

    /// <summary>Lowest perceived brightness the slider allows (fully dimmed).</summary>
    public const double MinBrightness = 0d;

    /// <summary>Highest perceived brightness: no dimming at all.</summary>
    public const double MaxBrightness = 100d;

    /// <summary>
    /// Warmest colour temperature offered, in kelvin (deep amber).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Blue is already fully extinguished by about 2000 K, so everything below
    /// that is about removing green as well — the difference between an amber
    /// screen and a genuinely red one. At this floor green reaches zero too and
    /// the display emits red only.
    /// </para>
    /// <para>
    /// The value is where the model saturates: every temperature below it
    /// produces an identical ramp, so there is nothing further to offer.
    /// </para>
    /// <para>
    /// At the very bottom of the range anything drawn in green or blue goes
    /// black, which is the point but is also disorienting the first time. The
    /// panic hotkey exists for exactly this.
    /// </para>
    /// </remarks>
    public const int MinTemperatureKelvin = 800;

    /// <summary>Coolest colour temperature offered, in kelvin (blue-ish).</summary>
    public const int MaxTemperatureKelvin = 10000;

    /// <summary>D65 white point: the identity setting, no tint applied.</summary>
    public const int NeutralTemperatureKelvin = 6500;

    /// <summary>
    /// Hard ceiling for the dimming overlay's alpha. Never 1.0: a fully opaque
    /// black overlay would leave the user with no way to see the screen and
    /// find the slider again.
    /// </summary>
    public const double AbsoluteMaxOverlayOpacity = 0.95d;

    // --- Persisted state ---------------------------------------------------

    /// <summary>Version of the schema this file was written with.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Master switch: when false Redture applies nothing at all.</summary>
    public bool EffectsEnabled { get; set; } = true;

    /// <summary>
    /// Interface language, as a two-letter code. Anything unrecognised falls
    /// back to English rather than failing, so a hand-edited settings file
    /// cannot leave the app with no text at all.
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Perceived brightness, 0–100. The upper part of the range maps to real
    /// backlight brightness and the lower part to the software dimming overlay;
    /// see <c>BrightnessMapper</c> for where they meet.
    /// </summary>
    public double Brightness { get; set; } = MaxBrightness;

    /// <summary>Target colour temperature in kelvin.</summary>
    public int TemperatureKelvin { get; set; } = NeutralTemperatureKelvin;

    /// <summary>Whether the time-of-day schedule drives the temperature.</summary>
    public bool AutomationEnabled { get; set; }

    /// <summary>Configuration for that schedule.</summary>
    public Scheduling.ScheduleSettings Schedule { get; set; } = new();

    /// <summary>Whether Redture registers itself to start at logon.</summary>
    public bool StartWithSystem { get; set; }

    /// <summary>
    /// Whether every correction stands down while an application owns the whole
    /// screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On by default. Games and films are colour-graded work meant to be seen
    /// as their authors left them, and an overlay cannot appear above exclusive
    /// fullscreen in any case.
    /// </para>
    /// <para>
    /// Worth being a choice rather than a rule: somebody watching a film late
    /// at night may well prefer to keep the warmth.
    /// </para>
    /// </remarks>
    /// <value>
    /// Stored under its original name. The setting used to withdraw only the
    /// overlay, and renaming the key would quietly reset it for anybody who had
    /// turned it off.
    /// </value>
    [JsonPropertyName("suspendOverlayInFullscreen")]
    public bool SuspendInFullscreen { get; set; } = true;

    /// <summary>User-configurable cap on overlay opacity, clamped to
    /// <see cref="AbsoluteMaxOverlayOpacity"/>.</summary>
    public double MaxOverlayOpacity { get; set; } = 0.92d;

    /// <summary>
    /// Whether Redture has already adopted the display's own backlight level
    /// into <see cref="Brightness"/>.
    /// </summary>
    /// <remarks>
    /// On the very first run where backlight control is found, Redture reads
    /// the level the user already had and moves its slider to match, instead of
    /// pushing the monitor to full brightness because that happens to be the
    /// default. It only ever does this once — after that the slider is
    /// authoritative and adopting again would silently discard the user's
    /// setting on every launch.
    /// </remarks>
    public bool HardwareBrightnessAdopted { get; set; }

    /// <summary>
    /// Whether the user explicitly opted in to the Windows registry tweak that
    /// unlocks the full gamma ramp range (see docs/architecture.md, risk R1).
    /// Never set without a confirmation dialog: it needs admin rights and a
    /// re-login to take effect.
    /// </summary>
    public bool ExtendedGammaRangeOptIn { get; set; }

    /// <summary>
    /// Clamps every value into its valid range. Called after loading so a
    /// hand-edited or corrupted-but-parseable file can never push the app into
    /// an unusable state (a black screen, for instance).
    /// </summary>
    public void Normalize()
    {
        Brightness = Math.Clamp(Brightness, MinBrightness, MaxBrightness);
        TemperatureKelvin = Math.Clamp(TemperatureKelvin, MinTemperatureKelvin, MaxTemperatureKelvin);
        MaxOverlayOpacity = Math.Clamp(MaxOverlayOpacity, 0d, AbsoluteMaxOverlayOpacity);

        if (SchemaVersion is < 1 or > CurrentSchemaVersion)
        {
            SchemaVersion = CurrentSchemaVersion;
        }

        Schedule ??= new Scheduling.ScheduleSettings();
        Schedule.Normalize();
    }

    /// <summary>
    /// Copy used to hand a snapshot to background work. Deep where it needs to
    /// be: a shared <see cref="Schedule"/> reference would let the UI mutate
    /// settings mid-serialisation.
    /// </summary>
    public AppSettings Clone()
    {
        AppSettings copy = (AppSettings)MemberwiseClone();
        copy.Schedule = Schedule.Clone();
        return copy;
    }
}
