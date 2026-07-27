using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Redture.Core.Infrastructure;
using Redture.Core.Settings;

namespace Redture.App.Services;

/// <summary>
/// Owns the graceful shutdown sequence.
/// </summary>
/// <remarks>
/// Centralised because the order matters and will matter more with every stage:
/// pending settings must be flushed, the display state restored (gamma back to
/// linear, overlays torn down — stages 1 and 2), and only then may the process
/// exit. Anything that skips this path is treated as a crash by
/// <see cref="CleanShutdownSentinel"/>.
/// </remarks>
public sealed class ApplicationLifecycle
{
    private readonly ISettingsStore _settingsStore;
    private readonly CleanShutdownSentinel _sentinel;
    private readonly ControlPanelPresenter _presenter;
    private readonly DisplayCoordinator _coordinator;
    private readonly ILogger<ApplicationLifecycle> _logger;

    /// <summary>0 = running, 1 = shutdown already under way.</summary>
    private int _shutdownStarted;

    public ApplicationLifecycle(
        ISettingsStore settingsStore,
        CleanShutdownSentinel sentinel,
        ControlPanelPresenter presenter,
        DisplayCoordinator coordinator,
        ILogger<ApplicationLifecycle> logger)
    {
        _settingsStore = settingsStore;
        _sentinel = sentinel;
        _presenter = presenter;
        _coordinator = coordinator;
        _logger = logger;
    }

    /// <summary>
    /// Runs the shutdown sequence once, no matter how many times it is called
    /// (tray menu, window command and OS session-end can all race here).
    /// </summary>
    public async Task ShutdownAsync()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
        {
            return;
        }

        _logger.LogInformation("Shutdown requested.");

        try
        {
            await _settingsStore.FlushAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush settings during shutdown.");
        }

        // From here on the run counts as clean, even if closing windows throws.
        _sentinel.EndRun();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Corrections come off the screen before any window closes: window
            // handles belong to this thread, and the overlay must not outlive
            // the message loop that owns it.
            _coordinator.Dispose();
            _presenter.PrepareForShutdown();

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
            else
            {
                Environment.Exit(0);
            }
        });
    }
}
