using System.Runtime.InteropServices;

// Every P/Invoke in this assembly names a system library, so restrict the
// loader to System32 and nothing else.
//
// Without this, the default search order includes the directory the
// application was started from. Redture installs into
// %LOCALAPPDATA%\Programs\Redture, which the user — and therefore anything
// running as the user — can write to. user32, gdi32, advapi32 and shell32 are
// in the KnownDLLs list and are always resolved from System32 regardless, but
// dxva2, shcore and wtsapi32 are not: a planted dxva2.dll beside the executable
// would be loaded in preference to the real one, giving code execution inside
// this process.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
