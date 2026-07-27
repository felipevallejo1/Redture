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

        // From here on the run counts as clean, even if tearing down throws.
        _sentinel.EndRun();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            TearDown();

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

    /// <summary>
    /// Shutdown path for a Windows log-off, restart or power-off.
    /// </summary>
    /// <remarks>
    /// Synchronous on purpose: the OS gives an application a short, unwaited
    /// window to tidy up and then kills it. Handing control back to an async
    /// continuation would mean the process is gone before the continuation
    /// runs, which is precisely how a clean log-off ends up looking like a
    /// crash on the next boot.
    /// <para>
    /// Must be called on the UI thread, which is where the lifetime raises the
    /// event. It deliberately does not ask the lifetime to shut down — the OS
    /// is already doing that.
    /// </para>
    /// </remarks>
    public void ShutdownForSessionEnd()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
        {
            return;
        }

        _logger.LogInformation("The OS is ending the session; running the shutdown sequence synchronously.");

        try
        {
            _settingsStore.FlushAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush settings during session end.");
        }

        _sentinel.EndRun();
        TearDown();
    }

    /// <summary>
    /// Removes every correction from the screen and releases the OS resources
    /// backing them. Runs on the UI thread: window handles belong to the thread
    /// that created them, and the overlay must not outlive its message loop.
    /// </summary>
    private void TearDown()
    {
        try
        {
            _coordinator.Dispose();
            _presenter.PrepareForShutdown();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to tear down display corrections.");
        }
    }
}
