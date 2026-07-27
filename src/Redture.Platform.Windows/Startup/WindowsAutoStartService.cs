using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Logging;
using Redture.Platform.Abstractions.Startup;
using Redture.Platform.Windows.Interop;

namespace Redture.Platform.Windows.Startup;

/// <inheritdoc cref="IAutoStartService" />
/// <remarks>
/// <para>
/// Uses the per-user <c>Run</c> key rather than a scheduled task or a service.
/// It needs no administrator rights, it is visible to the user in Task
/// Manager's startup tab where they can turn it off without going looking for
/// Redture, and removing the application removes the entry with it.
/// </para>
/// <para>
/// The registered command line carries no arguments, so a launch at logon
/// starts silently in the tray. Opening the panel every time someone signs in
/// would make this a feature people disable.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsAutoStartService : IAutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Redture";

    private readonly ILogger<WindowsAutoStartService> _logger;

    public WindowsAutoStartService(ILogger<WindowsAutoStartService> logger) => _logger = logger;

    public bool IsSupported => true;

    public bool IsEnabled
    {
        get
        {
            string? registered = ReadRegisteredCommand();
            if (registered is null)
            {
                return false;
            }

            // Registered but pointing somewhere else means a stale entry from a
            // previous install location. Reporting that as "on" would leave the
            // user with a switch that looks enabled and starts nothing.
            string expected = BuildCommand();
            bool matches = string.Equals(registered, expected, StringComparison.OrdinalIgnoreCase);

            if (!matches)
            {
                _logger.LogInformation(
                    "The logon entry points at {Registered}, not at this build; treating auto-start as off.",
                    registered);
            }

            return matches;
        }
    }

    public bool SetEnabled(bool enabled)
    {
        int result = Advapi32.RegCreateKeyExW(
            Advapi32.HkeyCurrentUser,
            RunKeyPath,
            reserved: 0,
            classType: null,
            options: 0,
            Advapi32.KeyWrite,
            securityAttributes: 0,
            out nint key,
            out _);

        if (result != Advapi32.ErrorSuccess)
        {
            _logger.LogError("Could not open the logon key for writing (error {Error}).", result);
            return false;
        }

        try
        {
            if (enabled)
            {
                string command = BuildCommand();
                result = Advapi32.RegSetValueExW(
                    key,
                    ValueName,
                    reserved: 0,
                    Advapi32.RegString,
                    command,
                    (uint)((command.Length + 1) * sizeof(char)));

                if (result != Advapi32.ErrorSuccess)
                {
                    _logger.LogError("Could not register Redture to start at logon (error {Error}).", result);
                    return false;
                }

                _logger.LogInformation("Redture will start at logon: {Command}", command);
                return true;
            }

            result = Advapi32.RegDeleteValueW(key, ValueName);

            // Deleting something that was never there is the desired end state,
            // not a failure.
            if (result is not Advapi32.ErrorSuccess and not Advapi32.ErrorFileNotFound)
            {
                _logger.LogError("Could not remove the logon entry (error {Error}).", result);
                return false;
            }

            _logger.LogInformation("Redture will no longer start at logon.");
            return true;
        }
        finally
        {
            Advapi32.RegCloseKey(key);
        }
    }

    /// <summary>
    /// The command line to register. Quoted because the path routinely contains
    /// spaces, and an unquoted path with a space is the classic way to end up
    /// launching something else entirely.
    /// </summary>
    private static string BuildCommand() => $"\"{Environment.ProcessPath}\"";

    private string? ReadRegisteredCommand()
    {
        int result = Advapi32.RegOpenKeyExW(
            Advapi32.HkeyCurrentUser,
            RunKeyPath,
            options: 0,
            Advapi32.KeyRead,
            out nint key);

        if (result != Advapi32.ErrorSuccess)
        {
            return null;
        }

        try
        {
            uint size = 0;
            result = Advapi32.RegQueryValueExW(key, ValueName, reserved: 0, out uint valueType, null, ref size);

            if (result != Advapi32.ErrorSuccess || valueType != Advapi32.RegString || size == 0)
            {
                return null;
            }

            byte[] buffer = new byte[size];
            result = Advapi32.RegQueryValueExW(key, ValueName, reserved: 0, out _, buffer, ref size);

            if (result != Advapi32.ErrorSuccess)
            {
                return null;
            }

            // The stored length includes the terminating null; trim it rather
            // than handing a string with a stray \0 to a comparison.
            return Encoding.Unicode.GetString(buffer, 0, (int)size).TrimEnd('\0');
        }
        finally
        {
            Advapi32.RegCloseKey(key);
        }
    }
}
