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
/// A named <see cref="Mutex"/> is used because it is the one named
/// synchronisation primitive .NET supports on every target OS. Signalling the
/// already-running instance to pop its window open needs real IPC (a named pipe)
/// and arrives with the rest of the polish in stage 4; for now the second
/// launch simply exits.
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

    private readonly Mutex _mutex;

    private SingleInstanceGuard(Mutex mutex) => _mutex = mutex;

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

        if (acquired)
        {
            return new SingleInstanceGuard(mutex);
        }

        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
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
