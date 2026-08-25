# Architecture

## 1. What the app does

Redture applies two corrections to the desktop:

1. **Colour temperature** — warms or cools the whole screen, manually or on a
   time-of-day schedule with gradual transitions.
2. **Brightness** — reduces perceived brightness, continuing past the point
   where the monitor's own backlight control bottoms out.

Both are driven from a single tray icon and a two-slider panel.

## 2. The display pipeline

The central design decision is *which mechanism handles which effect*, and it
follows from the alpha-compositing equation rather than from convenience.

Blending a colour `T` at opacity `a` over screen content `C`:

```
result = (1 - a)·C + a·T
```

- **Black overlay** (`T = 0`) → `result = (1 - a)·C`. A pure multiply: black
  pixels stay black, contrast ratio is preserved, and there is no quantisation
  banding because the compositor works above 8 bits per channel.
- **Amber overlay** (`T = warm`) → the `a·T` term never vanishes. Black pixels
  become brown, the black level rises and contrast collapses.

Therefore:

| Effect | Mechanism | Stage of the pipeline |
|---|---|---|
| Colour temperature | Gamma ramp (LUT) | GPU scanout, before composition |
| Dimming below hardware minimum | Black layered overlay | Desktop compositor |
| Brightness within hardware range | DDC/CI, WMI backlight | Monitor firmware |

Because the LUT and the overlay act at different stages, they compose cleanly:
neither can produce the colour fringing or flicker that comes from two
processes fighting over the same buffer.

## 3. Platform APIs

### Windows (primary target)

**Colour temperature**

- `EnumDisplayDevices` → adapter output names (`\\.\DISPLAY1`).
- `CreateDC("DISPLAY", deviceName, …)` → one device context per display.
- `gdi32!SetDeviceGammaRamp` / `GetDeviceGammaRamp` with `WORD[3][256]`.
- Kelvin → RGB multipliers from an interpolated blackbody table (Planckian
  locus, 1000 K–25000 K), not a polynomial approximation: the table is
  noticeably more accurate in the 2700–4500 K band, which is where the app
  actually operates.
- HDR detection via `QueryDisplayConfig` +
  `DisplayConfigGetDeviceInfo(DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO)`.

**Dimming overlay**

- One window per display: `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST |
  WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`.
  - `LAYERED` → per-window alpha; `TRANSPARENT` → click-through;
    `TOOLWINDOW` → hidden from Alt-Tab and the taskbar; `NOACTIVATE` → never
    steals focus.
- `SetLayeredWindowAttributes(hwnd, 0, alpha, LWA_ALPHA)` over a black
  background. Changing alpha is a compositor operation: no repaint, no flicker,
  effectively no CPU.
- `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)` so the overlay does
  not appear in screenshots or screen sharing.
- React to `WM_DISPLAYCHANGE`, `WM_DPICHANGED`, `SystemEvents.DisplaySettingsChanged`
  and session lock/unlock. Per-Monitor v2 DPI awareness is declared in
  `app.manifest`.

**Hardware brightness**

- External monitors: `dxva2.dll` → `GetPhysicalMonitorsFromHMONITOR` +
  `SetMonitorBrightness` (DDC/CI over the cable's I²C channel).
- Laptop panels: WMI `root\WMI` → `WmiMonitorBrightnessMethods.WmiSetBrightness`.

**Supporting**

- Auto-start: `HKCU\…\CurrentVersion\Run` — no admin rights, no scheduled task.
- Single instance: named `Mutex`.
- Panic hotkey: `RegisterHotKey`.

### Linux (stage 5)

- X11: `XRRSetCrtcGamma`; overlay as an override-redirect window with
  `_NET_WM_WINDOW_TYPE_NOTIFICATION` and input pass-through via XShape.
- Wayland (wlroots — Sway, Hyprland): `wlr-gamma-control-unstable-v1` and
  `wlr-layer-shell`.
- Wayland (GNOME, KDE): no third-party gamma protocol exists. The app degrades
  with an explicit message rather than failing silently.

### macOS (stage 6)

- `CGSetDisplayTransferByTable` for gamma; borderless `NSWindow` at
  `.screenSaver` level with `ignoresMouseEvents` for the overlay.

## 4. Transition engine

One engine in `Redture.Core` owns every value change, under four rules that
exist specifically to prevent flicker:

1. **Interpolate in mired** (`10⁶/K`), not in kelvin. Mired is perceptually
   uniform; interpolating kelvin linearly makes a sunset transition feel abrupt
   at the start and stalled at the end.
2. **Adaptive tick.** 20 Hz only while a transition is running; one tick per
   30 s at rest to re-evaluate the schedule. No permanent high-frequency timer.
3. **Idempotent writes.** Compute the new LUT, compare against the one already
   applied, and skip the syscall when identical. Redundant `SetDeviceGammaRamp`
   calls are the single most common cause of flicker in this class of app.
4. **One writer.** All OS writes are serialised through a single component.
   Two threads touching the LUT is not a race to be optimised — it is a
   visible artefact.

## 5. Risk register

| # | Risk | Severity | Mitigation |
|---|---|---|---|
| R1 | Windows clamps gamma ramps that deviate too far from linear, so aggressive warm settings are rejected or flattened | High | Optional registry opt-in (`…\ICM\GdiIcmGammaRange = 256`, admin + re-login). Never applied silently; without it the offered range is clamped and the UI says so |
| R2 | HDR / auto colour management makes the GDI LUT a no-op | High | Detect per display at startup and on every display change; disable temperature for that display with a clear message instead of failing quietly |
| R3 | Fullscreen applications sit above the overlay and reset the gamma ramp | High | Stand down rather than fight z-order, and re-apply on return to the desktop. Only the tint withdraws. The brightness is the level the user chose for the room they are in, and a game starting is no reason to undo it; dropping the overlay as well would brighten the screen for anybody dimmed below the point where the backlight runs out. Detected by inspecting the **foreground window** — owning process, window class, and whether it covers its monitor. `SHQueryUserNotificationState` alone is unusable: its `QUNS_BUSY` state is what borderless fullscreen reports, and Redture's own overlay is a borderless fullscreen window, so acting on it made the overlay switch itself on and off indefinitely. The shell query is still consulted for exclusive Direct3D and presentation mode, which the overlay cannot cause. Suspension is a user-facing setting, since somebody watching a film may want the dimming to stay |
| R4 | f.lux, Night Light, Iris or the GPU control panel writing the same LUT | High | Detect known processes and the Night Light registry state at startup; warn actionably. Re-check periodically with backoff instead of entering a write war |
| R5 | Gamma is global driver state and survives a crash, leaving an orange screen | High | Sentinel file plus `ProcessExit` / `UnhandledException` / `SessionEnding` handlers, and a global panic hotkey that restores a linear ramp |
| R6 | Deep dimming plus a hung app leaves an unusable screen | High | Hard opacity cap below 1.0, panic hotkey, and the settings window always above the overlay |
| R7 | Multi-monitor: mixed DPI, hot-plug, negative coordinates, burst change events | Medium | Per-Monitor v2 awareness, one overlay per display, debounced rebuild on `WM_DISPLAYCHANGE` (rebuilding per event is itself a flicker source) |
| R8 | Windows resets gamma on UAC / lock / RDP / session switch | Medium | Re-apply on session change with a short delay |
| R9 | Overlay showing up in recordings and screen shares | Medium | `WDA_EXCLUDEFROMCAPTURE` |
| R10 | 8-bit banding if dimming were done through the LUT | Medium | Avoided by design: the LUT only carries colour, the overlay only carries dimming |
| R11 | SmartScreen / AV heuristics on an unsigned binary that registers hotkeys and creates invisible topmost windows | Medium | **Resolved by not distributing binaries.** Signing costs a few hundred dollars a year; without it, a download that trips SmartScreen asks a stranger to decide whether a screen dimmer is malware, on no evidence either way. Shipping source and a one-command install script removes the question instead of answering it badly |
| R12 | DDC/CI unsupported or broken depending on monitor, cable or GPU | Low | Probe at startup; on failure the hardware segment of the slider collapses and the full range becomes overlay-driven |

## 6. Module map

```
Redture.Core                      no OS APIs, no UI framework
  Infrastructure/                 paths, crash sentinel
  Settings/                       AppSettings, atomic JSON store
  Color/          (stage 2)       blackbody table, gamma ramp builder
  Brightness/     (stage 1.5)     slider → (backlight %, overlay alpha)
  Scheduling/     (stage 3)       solar calculator, schedule evaluator
  Transitions/    (stage 3)       mired interpolation, easing
  Overrides/      (stage 3)       pause 1 h, cinema mode

Redture.Platform.Abstractions     the porting surface
  Displays/                       DisplayInfo, IDisplayEnumerator
  Gamma/          (stage 2)       IGammaController
  Overlay/        (stage 1)       IOverlayController
  Brightness/     (stage 1.5)     IHardwareBrightnessController

Redture.Platform.Windows          P/Invoke backends, one file per DLL
Redture.Platform.Linux            (stage 5)
Redture.Platform.MacOS            (stage 6)

Redture.App                       Avalonia: tray, panel, composition root
```

## 7. Resource budget

Redture runs for weeks at a time, so the targets are explicit:

- No timer running when nothing is changing.
- Overlay alpha changes go through the compositor, not through repaints.
- Log directory capped (7 days, 8 MB per file).
- Settings writes debounced to one per 750 ms of idle input.
