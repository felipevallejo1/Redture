namespace Redture.Core.Infrastructure;

/// <summary>
/// Resolves the on-disk locations Redture writes to. Abstracted so tests can
/// redirect everything to a temporary folder instead of the real user profile.
/// </summary>
public interface IAppPaths
{
    /// <summary>Root folder for all persistent user data.</summary>
    string DataDirectory { get; }

    /// <summary>Full path of the JSON settings file.</summary>
    string SettingsFilePath { get; }

    /// <summary>Folder holding rolling log files.</summary>
    string LogDirectory { get; }

    /// <summary>
    /// Marker file used to detect an unclean shutdown. It is created at startup
    /// and deleted on a graceful exit; finding it on launch means the previous
    /// run crashed and any gamma ramp it applied must be reset to linear.
    /// </summary>
    string CleanShutdownSentinelPath { get; }

    /// <summary>Creates every directory above if it does not exist yet.</summary>
    void EnsureCreated();
}
