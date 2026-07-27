namespace Redture.Core.Settings;

/// <summary>
/// Loads and persists <see cref="AppSettings"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Current"/> is a single long-lived instance that the view models
/// mutate in place. Callers signal a change with <see cref="RequestSave"/>,
/// which coalesces bursts of writes — dragging a slider raises hundreds of
/// property changes per second and none of them should hit the disk.
/// </para>
/// </remarks>
public interface ISettingsStore
{
    /// <summary>
    /// The live settings instance. Replaced only once, by <see cref="LoadAsync"/>
    /// during startup and before anything binds to it; after that it is mutated
    /// in place so bindings stay attached.
    /// </summary>
    AppSettings Current { get; }

    /// <summary>
    /// Reads the settings file. A missing file yields defaults; an unparseable
    /// one is backed up and replaced by defaults, so loading never throws for
    /// bad file content.
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Schedules a debounced write of <see cref="Current"/>.</summary>
    void RequestSave();

    /// <summary>Writes immediately, bypassing the debounce.</summary>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes if a save is still pending. Called on shutdown so the last slider
    /// movement is never lost.
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
