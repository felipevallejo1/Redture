namespace Redture.Core.Color;

/// <summary>
/// A display's colour lookup table: 256 entries per channel, 16 bits each.
/// </summary>
/// <remarks>
/// <para>
/// The layout — red, then green, then blue, contiguous — is exactly what
/// Windows' <c>SetDeviceGammaRamp</c> expects, so the array can be handed to
/// the OS with no copying or repacking.
/// </para>
/// <para>
/// This is where colour temperature is applied, and nothing else. Brightness
/// deliberately does not travel through the LUT: it is 8 bits in and 8 bits
/// out, so scaling it down to dim would compress the whole tonal range into a
/// few dozen levels and band visibly in exactly the dark content a night-time
/// dimmer is used for.
/// </para>
/// </remarks>
public sealed class GammaRamp
{
    /// <summary>Entries per channel.</summary>
    public const int LevelsPerChannel = 256;

    /// <summary>Number of channels.</summary>
    public const int Channels = 3;

    /// <summary>Largest value a ramp entry can hold.</summary>
    public const ushort MaxValue = ushort.MaxValue;

    private GammaRamp(ushort[] values) => Values = values;

    /// <summary>
    /// The raw table, <see cref="Channels"/> × <see cref="LevelsPerChannel"/>
    /// entries.
    /// </summary>
    public ushort[] Values { get; }

    /// <summary>
    /// The identity ramp: output equals input, no correction at all. This is
    /// what a display is restored to on shutdown, and what the recovery path
    /// forces after an unclean run.
    /// </summary>
    public static GammaRamp Linear { get; } = Create(1d, 1d, 1d);

    /// <summary>
    /// Builds a ramp from per-channel gains applied to the encoded values.
    /// </summary>
    public static GammaRamp Create(double redGain, double greenGain, double blueGain)
    {
        ushort[] values = new ushort[Channels * LevelsPerChannel];
        Span<double> gains = [redGain, greenGain, blueGain];

        for (int channel = 0; channel < Channels; channel++)
        {
            double gain = Math.Clamp(gains[channel], 0d, 1d);
            int offset = channel * LevelsPerChannel;

            for (int level = 0; level < LevelsPerChannel; level++)
            {
                double normalised = level / (double)(LevelsPerChannel - 1);
                values[offset + level] = (ushort)Math.Clamp(
                    Math.Round(normalised * gain * MaxValue),
                    0d,
                    MaxValue);
            }
        }

        return new GammaRamp(values);
    }

    /// <summary>
    /// Whether two ramps hold identical values.
    /// </summary>
    /// <remarks>
    /// The single most effective flicker guard in the whole application:
    /// re-sending a ramp the driver already has is the classic cause of the
    /// visible stutter these tools are known for, and a transition passes
    /// through the same rounded table many times over.
    /// </remarks>
    public bool HasSameValues(GammaRamp other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Values.AsSpan().SequenceEqual(other.Values);
    }

    /// <summary>Reads one entry, for tests and diagnostics.</summary>
    public ushort this[int channel, int level] => Values[(channel * LevelsPerChannel) + level];
}
