using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Redture.Platform.Windows.Interop;

/// <summary>
/// P/Invoke declarations for <c>advapi32.dll</c> — the registry.
/// </summary>
/// <remarks>
/// Used instead of <c>Microsoft.Win32.Registry</c>: on a neutral
/// <c>net9.0</c> target that type lives in a legacy compatibility package, and
/// pulling in a deprecated dependency to read one DWORD is a poor trade when
/// the rest of this assembly is P/Invoke anyway.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class Advapi32
{
    internal static readonly nint HkeyLocalMachine = unchecked((nint)0x80000002);

    internal const uint KeyRead = 0x20019;
    internal const uint KeyWrite = 0x20006;
    internal const uint RegDword = 4;
    internal const int ErrorSuccess = 0;

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    internal static extern int RegOpenKeyExW(
        nint hKey,
        string subKey,
        uint options,
        uint desiredAccess,
        out nint result);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    internal static extern int RegCreateKeyExW(
        nint hKey,
        string subKey,
        uint reserved,
        string? classType,
        uint options,
        uint desiredAccess,
        nint securityAttributes,
        out nint result,
        out uint disposition);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    internal static extern int RegQueryValueExW(
        nint hKey,
        string valueName,
        nint reserved,
        out uint valueType,
        out uint data,
        ref uint dataSize);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    internal static extern int RegSetValueExW(
        nint hKey,
        string valueName,
        uint reserved,
        uint valueType,
        ref uint data,
        uint dataSize);

    [DllImport("advapi32.dll")]
    internal static extern int RegCloseKey(nint hKey);
}
