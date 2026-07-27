namespace Redture.Platform.Abstractions.Startup;

/// <summary>
/// Registers Redture to start when the user signs in.
/// </summary>
/// <remarks>
/// A colour and brightness correction that has to be launched by hand is a
/// correction that will not be running at the moment it matters most — late at
/// night, on a machine that was just rebooted. This is the difference between
/// something you use and something you install.
/// </remarks>
public interface IAutoStartService
{
    /// <summary>Whether this platform has an auto-start mechanism wired up.</summary>
    bool IsSupported { get; }

    /// <summary>Whether Redture is currently registered to start at logon.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Registers or unregisters. Returns whether the change actually took, so
    /// the UI can reflect reality rather than the request.
    /// </summary>
    bool SetEnabled(bool enabled);
}
