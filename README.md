<div align="center">
  <img src="docs/images/icon.png" width="96" alt="Redture" />
  <h1>Redture</h1>
  <p><strong>Colour temperature and below-hardware-minimum brightness, in one control.</strong></p>
  <p>
    <img src="https://img.shields.io/badge/.NET-9.0-512BD4" alt=".NET 9" />
    <img src="https://img.shields.io/badge/UI-Avalonia%2011-8B44AC" alt="Avalonia 11" />
    <img src="https://img.shields.io/badge/license-MIT-green" alt="MIT" />
  </p>
</div>

---

Every laptop has a brightness slider that stops being useful two steps above
where you actually want it at 2 a.m. Redture keeps going past that point, and
warms the screen on a schedule while it's at it — a tray app that combines
what f.lux and a dimmer would each do separately, without them fighting each
other over the same display state.

## The idea worth explaining

A tinted overlay is the obvious way to build this, and it is the wrong one.

Alpha-blending a colour `T` at opacity `a` over the screen content `C` gives:

```
result = (1 - a)·C + a·T
```

Substitute a **black** overlay (`T = 0`) and the term vanishes:

```
result = (1 - a)·C          →  a pure multiply. Black stays black.
```

Substitute an **amber** overlay and it does not. Every black pixel becomes
`a·T` — a muddy brown. Contrast collapses, and that is exactly the "weird
colour banding" people notice in naive blue-light filters.

So Redture splits the job by the maths, not by convenience:

| Effect | Mechanism | Why |
|---|---|---|
| Colour temperature | Gamma ramp / LUT, applied before scanout | Per-channel transform with no contrast loss and no compositing cost |
| Dimming below the hardware minimum | Black click-through overlay | A true multiply — preserves black level, no 8-bit banding |
| Brightness within hardware range | DDC/CI and WMI backlight control | Real backlight is easier on the eyes and on the battery than dimming pixels |

The two run at different stages of the display pipeline — the LUT in the GPU's
scanout, the overlay in the desktop compositor — which is why they combine
without artefacts.

And because nobody should have to care which subsystem is responsible, the two
brightness mechanisms sit behind **one** slider: the upper part drives the real
backlight, and when that bottoms out the overlay picks up seamlessly. On first
run Redture adopts whatever level the display already had rather than
overwriting it, and it restores that level when you quit.

### Detecting a fight over the LUT

The colour lookup table is one global slot with no ownership and no
notification. Two colour tools writing it simply take turns, and the user sees
the screen flicker between two tints.

Redture finds this out by **reading the table back**, not by looking for known
applications in the process list. A process-name check only recognises the tools
someone thought to enumerate; a read-back catches anything that writes the LUT,
including a vendor control panel nobody anticipated. The process list is used
only to put a name in the message.

And once a conflict is confirmed, Redture **stops**. Re-applying would win the
next round and lose the one after, turning a static conflict into a visible
ping-pong. Reporting it once and stepping back leaves a stable screen and an
explanation the user can act on.

### Failing loudly instead of silently

On a display in HDR mode, loading a gamma ramp **succeeds and does nothing**.
The call returns success, the driver stores the table, and the picture never
changes. There is no way to tell that apart from working correctly by looking
at return codes alone, so Redture asks the display configuration API directly
and says which displays it cannot affect — rather than leaving a slider that
appears to work.

## Status

Built in stages, each one shippable on its own.

| Stage | Scope | Status |
|---|---|---|
| 0 | Scaffolding: tray, DI, logging, settings, display enumeration, crash detection | ✅ done |
| 1 | Dimming overlay (Windows), multi-monitor, capture exclusion, panic hotkey | ✅ done |
| 1.5 | Real backlight control (DDC/CI + WMI), unified two-segment slider | ✅ done |
| 2 | Colour temperature via gamma ramp, conflict detection, gamma-range opt-in | ✅ done |
| 2.5 | HDR detection, so the tint fails loudly instead of silently | ✅ done |
| 3 | Time-of-day automation, solar schedule, manual overrides | ✅ done |
| 4 | Auto-start, fullscreen detection, tray polish | ✅ done |
| 5 | Linux: X11 display enumeration and colour temperature | ✅ done |
| 5.5 | Linux dimming overlay | ⬜ |
| 6 | macOS colour temperature (written, unverified — needs a Mac) | ⚠️ |
| 7 | Packaging and release | ⬜ next |

## Platform support

| Platform | Colour temperature | Dimming | Backlight | Notes |
|---|---|---|---|---|
| Windows 10/11 | ✅ `SetDeviceGammaRamp` | ✅ layered click-through window | ✅ DDC/CI + WMI | Primary target. Gamma is ignored while HDR is on, and Redture says so |
| Linux — X11 | ✅ `XRRSetCrtcGamma` | ⬜ not yet | ⬜ not yet | Verified against a real X server. Ramp size is read per CRTC rather than assumed |
| Linux — Wayland | ❌ | ❌ | ⬜ | No standard protocol exists for setting a colour lookup table. `wlr-gamma-control` is a wlroots extension, so it would cover Sway and Hyprland but not GNOME or KDE |
| macOS | ⚠️ `CGSetDisplayTransferByTable` | ⬜ not yet | ⬜ not yet | **Written but never executed** — no Mac was available, and macOS cannot be containerised the way Linux was. Every failure path degrades to "unsupported" rather than throwing |

On a platform where something is missing, Redture runs with a smaller feature
set and reports what is unavailable — it does not pretend, and it does not
refuse to start. `dotnet run --project tools/Redture.Diagnostics` prints exactly
what the current machine supports.

## Download

Binaries are attached to each [release](https://github.com/felipevallejo1/Redture/releases),
built by CI from a clean checkout of the tagged commit.

| File | What it is |
|---|---|
| `Redture.exe` | Windows, ~94 MB. Download and run. Nothing to install, no .NET needed |
| `Redture-win-x64.zip` | The same executable, ~40 MB. Worth it on a slow connection |
| `Redture-linux-x64.tar.gz` | Linux, X11 only. Colour temperature works; dimming does not yet |
| `SHA256SUMS.txt` | Checksums for all of the above |

### Windows will warn you, and it is right to

The binaries are **not code-signed**, so the first launch shows *"Windows
protected your PC"*. Signing needs a certificate that costs a few hundred
dollars a year, which is hard to justify for a project like this.

Click **More info → Run anyway** if you trust it — and it is entirely reasonable
not to. The alternative is to build it yourself from source, which takes about
fifteen seconds and is the option this repository is really for.

Before running it either way, check what you downloaded:

```powershell
Get-FileHash Redture.exe -Algorithm SHA256
```

and compare against `SHA256SUMS.txt`.

## Build and run

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/felipevallejo1/Redture.git
cd Redture
dotnet build
dotnet test
dotnet run --project src/Redture.App -- --show
```

Redture starts in the system tray. `--show` opens the control panel on launch;
without it the app starts silently, which is how it will run at logon.

**Press `Ctrl + Alt + Shift + R` at any time** to reset brightness and colour
back to neutral. That shortcut is the escape hatch: dimming can go dark enough
that finding the slider again is genuinely hard.

User data lives in `%APPDATA%\Redture` (`~/.config/Redture` on Linux):
`settings.json` and rolling logs under `logs/`. Set `REDTURE_DATA_DIR` to
relocate both — useful for a portable install.

## Project layout

```
src/
  Redture.Core                    Platform-agnostic logic. References no OS API,
                                  no UI framework. Fully unit-testable.
  Redture.Platform.Abstractions   Contracts between Core/App and the OS backends.
  Redture.Platform.Windows        Windows backend (user32, gdi32, dxva2, …).
  Redture.Platform.Linux          Linux backend (libX11, libXrandr).
  Redture.Platform.MacOS          macOS backend (Core Graphics). Unverified.
  Redture.App                     Avalonia UI: tray icon, control panel, DI root.
tools/
  Redture.Diagnostics             Prints what this machine actually supports.
tests/
  Redture.Core.Tests              111 unit tests for Core.
docs/
  architecture.md                 Design, APIs used, known risks.
  adr/                            Architecture Decision Records.
```

Porting Redture to a new OS means implementing the interfaces in
`Redture.Platform.Abstractions`. Nothing else changes.

## What has actually been verified

Colour and display code is easy to write and hard to be sure about, so this is
tracked explicitly rather than assumed.

| Claim | How it was checked |
|---|---|
| The gamma ramp reaching the driver is the one the maths produced | Read back through `GetDeviceGammaRamp` and compared entry by entry — all 768 match |
| The overlay is a pure multiply that preserves black | Screen luminance measured with the overlay on and off; `WDA_EXCLUDEFROMCAPTURE` confirmed by the capture being unchanged |
| Brightness splits correctly across backlight and overlay | Independent DDC/CI reader confirmed 50 % backlight at slider 65, and overlay alpha 117 at slider 15 |
| Multi-monitor, hot-plug and negative coordinates | Overlay controller driven with a fabricated three-display topology; the real windows it created were inspected through Win32 |
| Display-change storms do not cause repeated rebuilds | Three `WM_DISPLAYCHANGE` messages in 200 ms produced exactly one rebuild |
| The colour model is correct | Cross-checked against an independent derivation of the Planckian locus, agreeing within 0.006 in CIE xy |
| Sunrise and sunset | Checked against properties of the solar system, plus absolute solar noon for five cities |
| The X11 backend | Run against a real X server in a container: enumeration, gamma write, and read-back reporting `Matches` |
| The macOS backend | **Not verified.** No Mac was available. CI runs the diagnostics tool on a macOS runner, which is as close as this gets until someone runs it on real hardware |

## Design notes

- [Why Avalonia instead of WPF](docs/adr/0001-avalonia-over-wpf.md)
- [Why gamma for colour and an overlay for brightness](docs/adr/0002-gamma-for-colour-overlay-for-brightness.md)
- [Why one brightness slider spans hardware and software](docs/adr/0003-unified-brightness-slider.md)
- [Full architecture and risk register](docs/architecture.md)

## License

MIT — see [LICENSE](LICENSE).
