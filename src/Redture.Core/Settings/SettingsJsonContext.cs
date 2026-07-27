using System.Text.Json.Serialization;

namespace Redture.Core.Settings;

/// <summary>
/// Source-generated serialisation metadata for <see cref="AppSettings"/>.
/// Using the generator instead of reflection keeps startup fast and leaves the
/// door open for trimming / NativeAOT in the packaging stage.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
