namespace Redture.App.Infrastructure;

/// <summary>
/// Makes sure only one Redture process runs at a time.
/// </summary>
/// <remarks>
/// Two instances would fight over the same global display state — each writing
/// its own gamma ramp and stacking a second dimming overlay — which is exactly
/// the flicker-inducing scenario the design tries to avoid with other vendors'
/// software, so it must not happen with ourselves.
/// <para>
/// A named <see cref="Mutex"/> holds the slot, because it is the one named
/// synchronisation primitive .NET supports on every target OS. A second named
/// handle carries the "someone tried to launch me again" signal, so that
/// double-clicking the executable opens the panel instead of appearing to do
/// nothing at all.
/// </para>
/// </remarks>
public sealed class SingleInstanceGuard : IDisposable
{
    /// <summary>
    /// Session-local name. Deliberately not <c>Global\</c>: per-user is the
    /// right scope (two users on the same machine each get their own instance)
    /// and it avoids needing extra privileges.
    /// </summary>
    private const string MutexName = @"Local\Redture.SingleInstance.9f2c1f7a";

    /// <summary>
    /// Set by a second launch to ask the running instance to show itself.
    /// </summary>
    /// <remarks>
    /// Named event handles are a Windows-only facility in .NET, so this whole
    /// mechanism degrades to "the second launch exits quietly" elsewhere. That
    /// is the existing behaviour, not a regression, and the Linux and macOS
    /// backends can bring their own signalling when they arrive.
    /// </remarks>
    private const string ActivationEventName = @"Local\Redture.Activate.9f2c1f7a";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle? _activationSignal;
    private readonly CancellationTokenSource _listenerShutdown = new();

    private SingleInstanceGuard(Mutex mutex, EventWaitHandle? activationSignal)
    {
        _mutex = mutex;
        _activationSignal = activationSignal;
    }

    /// <summary>
    /// Raised when another launch asked this instance to come to the front.
    /// Fires on a background thread.
    /// </summary>
    public event EventHandler? ActivationRequested;

    /// <summary>
    /// Returns a guard when this process owns the single-instance slot, or
    /// <see langword="null"/> when another instance already holds it.
    /// </summary>
    public static SingleInstanceGuard? TryAcquire()
    {
        Mutex mutex = new(initiallyOwned: false, MutexName);

        bool acquired;
        try
        {
            acquired = mutex.WaitOne(TimeSpan.Zero, exitContext: false);
        }
        catch (AbandonedMutexException)
        {
            // The previous owner died without releasing. We now hold the mutex,
            // and the crash itself is reported separately by CleanShutdownSentinel.
            acquired = true;
        }

        if (!acquired)
        {
            SignalRunningInstance();
            mutex.Dispose();
            return null;
        }

        SingleInstanceGuard guard = new(mutex, TryCreateActivationSignal());
        guard.StartListening();
        return guard;
    }

    /// <summary>Asks the instance that already holds the slot to show itself.</summary>
    private static void SignalRunningInstance()
    {
        // The platform-compatibility analyzer insists on this guard, and it is
        // right to: named event handles exist only on Windows, and without the
        // check this would be a runtime exception on the first Linux run.
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            if (EventWaitHandle.TryOpenExisting(ActivationEventName, out EventWaitHandle? signal))
            {
                using (signal)
                {
                    signal.Set();
                }
            }
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or UnauthorizedAccessException or IOException)
        {
            // Nothing to do: the other instance simply will not pop up. Failing
            // to say hello is not worth failing to exit over.
        }
    }

    private static EventWaitHandle? TryCreateActivationSignal()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return new EventWaitHandle(initialState: false, EventResetMode.AutoReset, ActivationEventName);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private void StartListening()
    {
        if (_activationSignal is null)
        {
            return;
        }

        Thread listener = new(() =>
        {
            WaitHandle[] handles = [_activationSignal, _listenerShutdown.Token.WaitHandle];

            while (WaitHandle.WaitAny(handles) == 0)
            {
                ActivationRequested?.Invoke(this, EventArgs.Empty);
            }
        })
        {
            IsBackground = true,
            Name = "Redture activation listener",
        };

        listener.Start();
    }

    public void Dispose()
    {
        _listenerShutdown.Cancel();
        _activationSignal?.Dispose();
        _listenerShutdown.Dispose();

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Not the owner (can happen if disposal races a crash); nothing to do.
        }

        _mutex.Dispose();
    }
}
