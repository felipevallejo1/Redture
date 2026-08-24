using System.Runtime.Versioning;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Redture.Platform.Abstractions.Brightness;
using Redture.Platform.Abstractions.Displays;

namespace Redture.Platform.Windows.Brightness;

/// <inheritdoc cref="IHardwareBrightnessController" />
/// <remarks>
/// <para>
/// The interesting part is the write path. A DDC/CI call takes about 60 ms and
/// blocks; a slider being dragged produces an event roughly every 16 ms. Sent
/// straight through, requests would queue up faster than the monitor can
/// consume them and the backlight would still be catching up seconds after the
/// user let go.
/// </para>
/// <para>
/// So writes go through a bounded channel of capacity one with
/// <see cref="BoundedChannelFullMode.DropOldest"/>: a new request overwrites
/// any request still waiting, and a single worker applies whatever it finds.
/// Intermediate slider positions are simply skipped — nobody wants to watch
/// them, they only want where the slider ended up.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsHardwareBrightnessController : IHardwareBrightnessController
{
    private readonly IDisplayEnumerator _displayEnumerator;
    private readonly ILogger<WindowsHardwareBrightnessController> _logger;

    /// <summary>
    /// Guards the target list. Discovery runs on the UI thread while the worker
    /// may be mid-write, and DDC/CI handles must not be released underneath it.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>Latest requested level; older pending values are discarded.</summary>
    private readonly Channel<double> _requests = Channel.CreateBounded<double>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    private readonly CancellationTokenSource _shutdown = new();

    /// <summary>
    /// The level each display was at before Redture touched it, keyed by
    /// display id and kept for the lifetime of the process. Guarded by
    /// <see cref="_gate"/> along with the targets it describes.
    /// </summary>
    private readonly Dictionary<string, double> _initialLevels = [];

    private DdcCiSession? _ddcSession;
    private WmiPanelBacklightTarget? _panelTarget;
    private List<BacklightTarget> _targets = [];
    private Task? _worker;
    private bool _disposed;

    public WindowsHardwareBrightnessController(
        IDisplayEnumerator displayEnumerator,
        ILogger<WindowsHardwareBrightnessController> logger)
    {
        _displayEnumerator = displayEnumerator;
        _logger = logger;
    }

    public bool IsAvailable
    {
        get
        {
            lock (_gate)
            {
                return _targets.Count > 0;
            }
        }
    }

    public IReadOnlyList<HardwareBrightnessTarget> Targets
    {
        get
        {
            lock (_gate)
            {
                return [.. _targets.Select(target => target.ToDescriptor())];
            }
        }
    }

    public double? CurrentPercent
    {
        get
        {
            lock (_gate)
            {
                // With several controllable displays their levels can differ;
                // the first one is representative enough for first-run adoption,
                // which is the only thing this feeds.
                return _targets.Count > 0 ? _targets[0].InitialPercent : null;
            }
        }
    }

    public void Refresh()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        IReadOnlyList<DisplayInfo> displays = _displayEnumerator.GetDisplays();

        lock (_gate)
        {
            ReleaseTargets();

            _ddcSession = DdcCiSession.Discover(displays, _logger);
            _targets = [.. _ddcSession.Targets];

            _panelTarget = WmiPanelBacklightTarget.TryDiscover(_logger);
            if (_panelTarget is not null)
            {
                _targets.Add(_panelTarget);
            }

            RememberOrRestoreInitialLevels();

            _logger.LogInformation(
                "Backlight control: {Count} controllable display(s).",
                _targets.Count);
        }

        EnsureWorkerStarted();
    }

    public void SetBrightness(double percent)
    {
        if (_disposed)
        {
            return;
        }

        // Never blocks: at worst this replaces a value the worker had not
        // picked up yet.
        _requests.Writer.TryWrite(Math.Clamp(percent, 0d, 100d));
    }

    public void RestoreInitial()
    {
        if (_disposed)
        {
            return;
        }

        lock (_gate)
        {
            foreach (BacklightTarget target in _targets)
            {
                // Force the write through: the cached level may well be the one
                // we are restoring to, and skipping it would leave the display
                // where Redture put it.
                target.ForgetLastWrite();
                target.ApplyPercent(target.InitialPercent, _logger);
            }
        }
    }

    private void EnsureWorkerStarted()
    {
        if (_worker is not null)
        {
            return;
        }

        _worker = Task.Run(async () =>
        {
            try
            {
                await foreach (double percent in _requests.Reader.ReadAllAsync(_shutdown.Token).ConfigureAwait(false))
                {
                    ApplyToTargets(percent);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The backlight worker stopped unexpectedly.");
            }
        });
    }

    private void ApplyToTargets(double percent)
    {
        lock (_gate)
        {
            foreach (BacklightTarget target in _targets)
            {
                target.ApplyPercent(percent, _logger);
            }
        }
    }

    /// <summary>Releases handles. The caller must hold <see cref="_gate"/>.</summary>
    /// <summary>
    /// Keeps each display's pre-Redture level across re-discovery.
    /// </summary>
    /// <remarks>
    /// The first time a display is seen, whatever it is showing is the user's
    /// own level and worth recording. Every time after that it is showing
    /// Redture's dimming, so the recorded value is the one to keep.
    /// </remarks>
    private void RememberOrRestoreInitialLevels()
    {
        foreach (BacklightTarget target in _targets)
        {
            if (_initialLevels.TryGetValue(target.DisplayId, out double remembered))
            {
                target.AdoptRememberedInitial(remembered);
            }
            else
            {
                _initialLevels[target.DisplayId] = target.InitialPercent;
            }
        }
    }

    private void ReleaseTargets()
    {
        _targets.Clear();

        _panelTarget?.Dispose();
        _panelTarget = null;

        _ddcSession?.Dispose();
        _ddcSession = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Hand the displays back before tearing anything down: quitting Redture
        // must never leave a monitor at a level the user did not choose.
        RestoreInitial();

        _disposed = true;
        _requests.Writer.TryComplete();
        _shutdown.Cancel();

        try
        {
            // Bounded: a DDC/CI write in flight takes tens of milliseconds, and
            // shutdown must not hang on a monitor that stopped answering.
            _worker?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Already logged inside the worker.
        }

        lock (_gate)
        {
            ReleaseTargets();
        }

        _shutdown.Dispose();
        _logger.LogDebug("Backlight controller disposed.");
    }
}
