# ADR 0002 — Gamma ramp for colour, overlay for brightness

**Status:** accepted — 2026-07-26

## Context

Two mechanisms are available for altering how the desktop looks, and the
tempting move is to pick one and use it for everything:

- A **semi-transparent overlay** window composited over the desktop.
- The **gamma ramp / LUT**, a per-channel lookup applied during scanout.

Using the overlay for both effects means tinting it amber. Using the LUT for
both means scaling it down to dim.

## Decision

Colour temperature goes through the gamma ramp. Dimming goes through a **black**
overlay. Neither does the other's job.

### Why not an amber overlay

Alpha compositing of colour `T` at opacity `a` over content `C`:

```
result = (1 - a)·C + a·T
```

With `T = 0` (black) the second term disappears and the expression becomes
`(1 - a)·C` — a pure multiply. Black stays black; contrast ratio is untouched.

With `T` amber, the term survives. Every black pixel becomes `a·T`: a muddy
brown, a raised black floor, collapsed contrast. This is the visible failure
mode of naive blue-light filters, and the requirement explicitly rules it out.

### Why not dim through the LUT

A gamma ramp can dim by scaling its entries, and the result is genuinely a
multiply. But the ramp is 8 bits in and 8 bits out: scaling it to 20 %
compresses the whole tonal range into ~50 distinct levels, which shows up as
banding in dark content — exactly where a night-time dimmer is used. The
compositor works at higher precision and does not have this problem.

### Why they combine cleanly

The LUT is applied by the display controller during scanout; the overlay is
composited by the desktop compositor before that. They occupy different stages
of the pipeline, so applying both is a composition of two independent
transforms rather than two processes contending for one buffer.

## Consequences

- Colour temperature inherits the LUT's constraints: unavailable while HDR is
  on (R2), clamped by Windows unless the user opts into the registry change
  (R1), reset by exclusive-fullscreen apps (R3) and by session changes (R8).
- Dimming inherits the overlay's constraints: invisible to exclusive-fullscreen
  apps (R3), and needs `WDA_EXCLUDEFROMCAPTURE` to stay out of screen shares (R9).
- The two failure modes are independent, which is a feature: HDR kills the tint
  but not the dimming, and a fullscreen game kills the dimming but the tint can
  be re-applied.
