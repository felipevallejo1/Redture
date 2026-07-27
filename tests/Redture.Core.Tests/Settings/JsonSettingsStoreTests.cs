using Microsoft.Extensions.Logging.Abstractions;
using Redture.Core.Infrastructure;
using Redture.Core.Settings;
using Xunit;

namespace Redture.Core.Tests.Settings;

/// <summary>
/// Covers the persistence guarantees the rest of the app relies on: settings
/// survive a restart, a damaged file never blocks startup, and a crash during a
/// write cannot corrupt the previous good file.
/// </summary>
public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly AppPaths _paths;

    public JsonSettingsStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "redture-tests", Guid.NewGuid().ToString("N"));
        _paths = new AppPaths(_directory);
    }

    private JsonSettingsStore CreateStore() =>
        new(_paths, NullLogger<JsonSettingsStore>.Instance);

    [Fact]
    public async Task LoadAsync_WithNoExistingFile_UsesDefaultsAndCreatesIt()
    {
        JsonSettingsStore store = CreateStore();

        await store.LoadAsync();

        Assert.Equal(AppSettings.MaxBrightness, store.Current.Brightness);
        Assert.Equal(AppSettings.NeutralTemperatureKelvin, store.Current.TemperatureKelvin);
        Assert.True(File.Exists(_paths.SettingsFilePath));
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsEveryValue()
    {
        JsonSettingsStore writer = CreateStore();
        await writer.LoadAsync();
        writer.Current.Brightness = 42.5;
        writer.Current.TemperatureKelvin = 3400;
        writer.Current.AutomationEnabled = true;
        writer.Current.EffectsEnabled = false;
        writer.Current.StartWithSystem = true;
        writer.Current.ExtendedGammaRangeOptIn = true;
        await writer.SaveAsync();

        JsonSettingsStore reader = CreateStore();
        await reader.LoadAsync();

        Assert.Equal(42.5, reader.Current.Brightness);
        Assert.Equal(3400, reader.Current.TemperatureKelvin);
        Assert.True(reader.Current.AutomationEnabled);
        Assert.False(reader.Current.EffectsEnabled);
        Assert.True(reader.Current.StartWithSystem);
        Assert.True(reader.Current.ExtendedGammaRangeOptIn);
    }

    [Fact]
    public async Task SaveAsync_LeavesNoTemporaryFileBehind()
    {
        JsonSettingsStore store = CreateStore();
        await store.LoadAsync();

        await store.SaveAsync();

        Assert.False(File.Exists(_paths.SettingsFilePath + ".tmp"));
    }

    [Fact]
    public async Task LoadAsync_WithCorruptFile_FallsBackToDefaultsAndQuarantinesIt()
    {
        _paths.EnsureCreated();
        await File.WriteAllTextAsync(_paths.SettingsFilePath, "{ this is not json");

        JsonSettingsStore store = CreateStore();
        await store.LoadAsync();

        Assert.Equal(AppSettings.MaxBrightness, store.Current.Brightness);
        Assert.NotEmpty(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    [Fact]
    public async Task LoadAsync_WithOutOfRangeValues_ClampsThem()
    {
        _paths.EnsureCreated();
        await File.WriteAllTextAsync(
            _paths.SettingsFilePath,
            """{ "brightness": 5000, "temperatureKelvin": -20, "maxOverlayOpacity": 1.0 }""");

        JsonSettingsStore store = CreateStore();
        await store.LoadAsync();

        Assert.Equal(AppSettings.MaxBrightness, store.Current.Brightness);
        Assert.Equal(AppSettings.MinTemperatureKelvin, store.Current.TemperatureKelvin);

        // The overlay can never be fully opaque: that would black the screen out
        // with no way for the user to find the slider again.
        Assert.Equal(AppSettings.AbsoluteMaxOverlayOpacity, store.Current.MaxOverlayOpacity);
    }

    [Fact]
    public async Task FlushAsync_WritesAPendingDebouncedSave()
    {
        JsonSettingsStore store = CreateStore();
        await store.LoadAsync();

        store.Current.Brightness = 12;
        store.RequestSave();      // Debounced: nothing on disk yet.
        await store.FlushAsync(); // Shutdown path must not lose it.

        JsonSettingsStore reader = CreateStore();
        await reader.LoadAsync();
        Assert.Equal(12, reader.Current.Brightness);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temp-folder cleanup is best effort.
        }
    }
}
