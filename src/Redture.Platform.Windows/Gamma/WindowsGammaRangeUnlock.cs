using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Redture.Platform.Abstractions.Gamma;
using Redture.Platform.Windows.Interop;

namespace Redture.Platform.Windows.Gamma;

/// <inheritdoc cref="IGammaRangeUnlock" />
/// <remarks>
/// <para>
/// The restriction lives in a single machine-wide DWORD. Reading it needs no
/// privileges; writing it needs administrator rights, so the change is made by
/// re-launching Redture elevated with a dedicated switch rather than by asking
/// the whole application to run as administrator forever.
/// </para>
/// <para>
/// The value is read by GDI once per session, which is why a sign-out is
/// required and why <see cref="GammaRangeState.UnlockedPendingSignOut"/> exists
/// as a distinct state: telling the user it worked while nothing has changed on
/// screen would be worse than telling them nothing.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsGammaRangeUnlock : IGammaRangeUnlock
{
    internal const string RegistryPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ICM";
    internal const string ValueName = "GdiIcmGammaRange";

    /// <summary>Value that lifts the restriction to the full ramp range.</summary>
    internal const int UnlockedValue = 256;

    /// <summary>Command-line switch handled by the elevated instance.</summary>
    public const string UnlockSwitch = "--unlock-gamma-range";

    private readonly ILogger<WindowsGammaRangeUnlock> _logger;

    /// <summary>
    /// Value present when the process started. Comparing against it is how a
    /// change made during this session — and therefore not yet in effect — is
    /// distinguished from one that was already active at sign-in.
    /// </summary>
    private readonly int? _valueAtStartup;

    public WindowsGammaRangeUnlock(ILogger<WindowsGammaRangeUnlock> logger)
    {
        _logger = logger;
        _valueAtStartup = ReadValue();
        Refresh();
    }

    public GammaRangeState State { get; private set; } = GammaRangeState.Unknown;

    public bool CanUnlock => State is GammaRangeState.Restricted;

    public void Refresh()
    {
        int? current = ReadValue();

        State = current switch
        {
            null => GammaRangeState.Restricted,
            UnlockedValue when current == _valueAtStartup => GammaRangeState.Unlocked,
            UnlockedValue => GammaRangeState.UnlockedPendingSignOut,
            _ => GammaRangeState.Restricted,
        };

        _logger.LogDebug("Gamma range state: {State} (registry value {Value}).", State, current);
    }

    public bool TryRequestUnlock()
    {
        string? executable = Environment.ProcessPath;
        if (executable is null)
        {
            _logger.LogError("Could not determine the executable path; cannot request elevation.");
            return false;
        }

        try
        {
            // UseShellExecute with the "runas" verb is what raises the UAC
            // prompt. The elevated instance writes the value and exits.
            ProcessStartInfo start = new()
            {
                FileName = executable,
                Arguments = UnlockSwitch,
                UseShellExecute = true,
                Verb = "runas",
            };

            using Process? elevated = Process.Start(start);
            elevated?.WaitForExit(TimeSpan.FromSeconds(30));

            Refresh();
            return true;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // Cancelling the UAC prompt lands here, and is a perfectly normal
            // answer rather than a failure worth alarming anyone about.
            _logger.LogInformation("The elevation request was declined or failed: {Reason}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Applies the registry change. Only ever called by an already-elevated
    /// instance started with <see cref="UnlockSwitch"/>.
    /// </summary>
    public static bool ApplyElevated(out string message)
    {
        int result = Advapi32.RegCreateKeyExW(
            Advapi32.HkeyLocalMachine,
            RegistryPath,
            reserved: 0,
            classType: null,
            options: 0,
            Advapi32.KeyWrite,
            securityAttributes: 0,
            out nint key,
            out _);

        if (result != Advapi32.ErrorSuccess)
        {
            message = $"Could not open the registry key for writing (error {result}). Administrator rights are required.";
            return false;
        }

        try
        {
            uint value = UnlockedValue;
            result = Advapi32.RegSetValueExW(key, ValueName, 0, Advapi32.RegDword, ref value, sizeof(uint));

            if (result != Advapi32.ErrorSuccess)
            {
                message = $"Could not write {ValueName} (error {result}).";
                return false;
            }

            message = $"{ValueName} set to {UnlockedValue}. Sign out and back in for it to take effect.";
            return true;
        }
        finally
        {
            Advapi32.RegCloseKey(key);
        }
    }

    private int? ReadValue()
    {
        int result = Advapi32.RegOpenKeyExW(
            Advapi32.HkeyLocalMachine,
            RegistryPath,
            options: 0,
            Advapi32.KeyRead,
            out nint key);

        if (result != Advapi32.ErrorSuccess)
        {
            _logger.LogDebug("Could not open the gamma range registry key (error {Error}).", result);
            return null;
        }

        try
        {
            uint data = 0;
            uint size = sizeof(uint);
            result = Advapi32.RegQueryValueExW(key, ValueName, reserved: 0, out uint valueType, out data, ref size);

            if (result != Advapi32.ErrorSuccess || valueType != Advapi32.RegDword)
            {
                // Absent is the normal case: Windows only writes this value when
                // somebody has deliberately changed it.
                return null;
            }

            return (int)data;
        }
        finally
        {
            Advapi32.RegCloseKey(key);
        }
    }
}
