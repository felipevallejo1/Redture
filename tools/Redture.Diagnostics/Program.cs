using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Redture.Core.Color;
using Redture.Platform.Abstractions.Brightness;
using Redture.Platform.Abstractions.Displays;
using Redture.Platform.Abstractions.Gamma;
using Redture.Platform.Linux;
using Redture.Platform.MacOS;
using Redture.Platform.Windows;

namespace Redture.Diagnostics;

/// <summary>
/// Reports what Redture can and cannot do on the machine it is run on.
/// </summary>
/// <remarks>
/// <para>
/// Exists because "it does not work on my display" is unanswerable without
/// knowing which of a dozen things was unavailable. Running this and pasting
/// the output turns that into a five-line bug report.
/// </para>
/// <para>
/// It composes the platform modules itself rather than borrowing the
/// application's composition root, which keeps it a test of the backends rather
/// than of the app that happens to use them.
/// </para>
/// </remarks>
internal static class Program
{
    public static int Main(string[] args)
    {
        bool applyTestRamp = args.Contains("--test-gamma", StringComparer.OrdinalIgnoreCase);

        using ServiceProvider services = Compose();

        Console.WriteLine($"Redture diagnostics");
        Console.WriteLine($"  runtime      : {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"  os           : {RuntimeInformation.OSDescription}");
        Console.WriteLine($"  architecture : {RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"  session      : {DescribeSession()}");
        Console.WriteLine();

        ReportDisplays(services);
        ReportGamma(services, applyTestRamp);
        ReportBacklight(services);
        ReportHdr(services);

        return 0;
    }

    private static ServiceProvider Compose()
    {
        ServiceCollection services = new();

        services.AddLogging(builder => builder
            .AddSimpleConsole(options => options.SingleLine = true)
            .SetMinimumLevel(LogLevel.Debug));

        if (OperatingSystem.IsWindows())
        {
            services.AddWindowsPlatform();
        }
        else if (OperatingSystem.IsLinux())
        {
            services.AddLinuxPlatform();
        }
        else if (OperatingSystem.IsMacOS())
        {
            services.AddMacOsPlatform();
        }

        services.TryAddSingleton<IDisplayEnumerator, UnsupportedDisplayEnumerator>();
        services.TryAddSingleton<IGammaController, NullGammaController>();
        services.TryAddSingleton<IHardwareBrightnessController, NullHardwareBrightnessController>();
        services.TryAddSingleton<IHdrDetector, NullHdrDetector>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Names the display server, which on Linux decides almost everything: X11
    /// is fully supported, Wayland has no standard way to set a colour lookup
    /// table at all.
    /// </summary>
    private static string DescribeSession()
    {
        if (!OperatingSystem.IsLinux())
        {
            return "native";
        }

        string? type = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        string? display = Environment.GetEnvironmentVariable("DISPLAY");
        string? wayland = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");

        return $"XDG_SESSION_TYPE={type ?? "unset"}, DISPLAY={display ?? "unset"}, WAYLAND_DISPLAY={wayland ?? "unset"}";
    }

    private static void ReportDisplays(IServiceProvider services)
    {
        IReadOnlyList<DisplayInfo> displays = services.GetRequiredService<IDisplayEnumerator>().GetDisplays();

        Console.WriteLine($"displays ({displays.Count})");

        foreach (DisplayInfo display in displays)
        {
            Console.WriteLine($"  {display}");
        }

        if (displays.Count == 0)
        {
            Console.WriteLine("  none detected");
        }

        Console.WriteLine();
    }

    private static void ReportGamma(IServiceProvider services, bool applyTestRamp)
    {
        IGammaController gamma = services.GetRequiredService<IGammaController>();

        Console.WriteLine("colour temperature");
        Console.WriteLine($"  backend      : {gamma.GetType().Name}");

        if (!applyTestRamp)
        {
            Console.WriteLine("  (pass --test-gamma to briefly apply a 3000 K ramp and restore it)");
            Console.WriteLine();
            return;
        }

        // Applied and immediately undone: enough to prove the write path works
        // without leaving the display somewhere the user did not ask for.
        gamma.Apply(GammaRampBuilder.Build(3000));
        Console.WriteLine($"  applied 3000K: supported={gamma.IsSupported} rejected={gamma.LastRampRejected}");
        Console.WriteLine($"  verification : {gamma.Verify()}");

        gamma.ResetToLinear();
        Console.WriteLine($"  restored     : supported={gamma.IsSupported}");
        Console.WriteLine();
    }

    private static void ReportBacklight(IServiceProvider services)
    {
        IHardwareBrightnessController backlight = services.GetRequiredService<IHardwareBrightnessController>();
        backlight.Refresh();

        Console.WriteLine("backlight");
        Console.WriteLine($"  backend      : {backlight.GetType().Name}");
        Console.WriteLine($"  available    : {backlight.IsAvailable}");

        foreach (HardwareBrightnessTarget target in backlight.Targets)
        {
            Console.WriteLine($"  {target.Name}: {target.Mechanism}, currently {target.InitialPercent:0}%");
        }

        Console.WriteLine();
    }

    private static void ReportHdr(IServiceProvider services)
    {
        IReadOnlySet<string> hdr = services.GetRequiredService<IHdrDetector>().FindHdrDisplays();

        Console.WriteLine("hdr");
        Console.WriteLine(hdr.Count == 0
            ? "  no displays in HDR mode"
            : $"  gamma ignored on: {string.Join(", ", hdr)}");
    }
}
