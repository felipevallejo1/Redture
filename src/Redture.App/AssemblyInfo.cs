using System.Runtime.InteropServices;

// Applies to the executable itself, for the same reason as in the platform
// assemblies: the application directory is user-writable, and it should never
// be a place the native loader looks for system libraries.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
