# ADR 0001 — Avalonia instead of WPF

**Status:** accepted — 2026-07-26

## Context

Redture must run on Windows first, with Linux and macOS as real goals rather
than aspirations. The author's existing experience is .NET 8/9, which makes WPF
the path of least resistance — and a dead end for the other two platforms.

Almost none of the work in this app is UI. It is P/Invoke into display APIs,
a colour-maths core and a scheduler; the interface is a tray icon and two
sliders. Whatever framework is chosen mostly has to stay out of the way and be
cheap to keep resident for weeks.

## Options considered

| Option | Cross-platform | Idle RAM | Native interop | Reuses .NET experience |
|---|---|---|---|---|
| **Avalonia 11 + .NET 9** | yes | ~50–70 MB | direct P/Invoke | yes, XAML close to WPF |
| WPF + .NET 9 | Windows only | ~40 MB | direct P/Invoke | yes |
| Electron + TypeScript | yes | ~150–250 MB | requires a native addon | no |
| Python + PyQt | yes | ~80 MB | ctypes, fragile | no |

## Decision

Avalonia 11 on .NET 9.

Electron was rejected on two counts: a quarter of a gigabyte resident for a
tray utility is indefensible, and the part of the app that matters would have
to be written in C++ anyway. Python's ctypes layer is workable but brittle for
struct-heavy Win32 signatures like `DEVMODEW`.

WPF stays viable as a fallback precisely because of how the solution is
structured: all logic lives in `Redture.Core` and `Redture.Platform.*`, so
swapping the UI project would not touch it.

Avalonia 11.3 is pinned rather than 12.x — nothing here needs Avalonia 12, and
11.3 has the wider body of documentation and examples.

## Consequences

- One UI codebase for all three targets; porting is implementing the interfaces
  in `Redture.Platform.Abstractions`.
- Slightly higher idle memory than WPF, and a smaller ecosystem for
  Windows-specific UI conveniences.
- The tray icon and window are Avalonia's; the overlay windows are **not** —
  they are raw `HWND`s, because they need window styles Avalonia does not
  expose and must be as close to free as possible.
