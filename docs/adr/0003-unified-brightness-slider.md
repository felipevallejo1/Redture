# ADR 0003 — One brightness slider across hardware and software

**Status:** accepted — 2026-07-26

## Context

Redture has two ways to make the screen darker:

1. Lower the actual backlight (DDC/CI for external monitors, WMI for laptop
   panels). Physically less light, less power, no effect on contrast.
2. Composite a black overlay. Works at any brightness level, including when the
   backlight is already at its minimum, but it dims pixels rather than light.

Hardware brightness is strictly better while it lasts. It just runs out — which
is the entire reason this project exists.

Exposing both as separate controls would be the honest engineering view and a
poor product: the user does not care which subsystem is responsible, they care
that the screen is too bright.

## Decision

One slider, 0–100, mapped across two segments:

```
100% ──────────────  backlight 100%,  overlay alpha 0
 30% ──────────────  backlight   0%,  overlay alpha 0
  0% ──────────────  backlight   0%,  overlay alpha 0.92 (safety cap)
```

Above the split the slider drives real backlight; below it the backlight is
already at zero and the overlay takes over. The split point and the opacity cap
are both configurable.

The mapping lives in `Redture.Core` as a pure function, so it is unit-testable
without a display attached.

## Consequences

- The user gets one control that keeps working past the hardware floor, with no
  discontinuity at the handover.
- The mapping must degrade when DDC/CI is unavailable (R12) — a monitor that
  refuses backlight control collapses the upper segment, and the whole range
  becomes overlay-driven. This has to be invisible in the UI beyond the loss of
  the power saving.
- Opacity is capped strictly below 1.0. A fully opaque overlay would leave the
  user with a black screen and no way to find the slider that caused it; the
  cap and the panic hotkey are the two guarantees against that.
