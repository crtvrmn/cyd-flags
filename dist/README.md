# Prebuilt artifacts

All images are built from `firmware/cyd-flags.yaml` with ESPHome 2026.10.2 /
ESP-IDF 5.5.6. They boot portrait (240×320); the plugin can rotate a display to
landscape at runtime, so the same image supports both mountings.

| file | use |
|------|-----|
| `cyd-flags-portrait-ili9341.factory.bin` | first USB flash, original single-USB CYD (ILI9341) |
| `cyd-flags-portrait-7789.factory.bin` | first USB flash, dual-USB CYD (ST7789V) |
| `cyd-flags-portrait-9342.factory.bin` | first USB flash, dual-USB CYD (ILI9342) |
| `*.ota.bin` | wireless update of the matching panel variant |
| `CydFlags.dll` | SimHub plugin 0.8.1, copy next to `SimHubWPF.exe` |

A factory flash erases the saved WiFi credentials; an OTA update does not.

0.8.1 fixes the SABT page (MinimumTension alias, WAITING state), keeps trend discs visible with a blue neutral state, and stops the ghost appearing in both tower rows; 0.8.0 adds the SABT belt-tensioner page and the card-based display UI; 0.7.12 adds the tower trend discs, removes the logic lap time and the rev bar, and lowers the heartbeat default; 0.7.11 exposes the firmware version in the web UI; 0.7.10 reads the UDP counter as a text entity (sensor endpoint broken on this ESPHome build); 0.7.9 sends flags over HTTP as well and shows an on-screen UDP warning; 0.7.8 adds an HTTP fallback until UDP is confirmed and repairs UDP port migration; 0.7.7 adds a UDP receive counter and plugin diagnosis; 0.7.6 adds an eight-second boot splash with the firmware version and always sends the HTTP heartbeat as a fallback, so `NO SIMHUB` does not appear when UDP is blocked; 0.7.5 moves realtime flags, game, tower, rev, logic, delta and heartbeat to UDP 18512 (HTTP only for configuration) so the tower data updates at 20 Hz; 0.7.4 makes the rev-limiter page use partial redraws at 20 Hz; 0.7.3 adds the global *Only show tower/logic/delta during a race* option, off by default, so the tower, race logic and delta bar also work in practice and qualifying; 0.7.2 corrected the mirrored touch X axis (alarm quadrants and corner navigation). 0.7.1 changes the connection
status to **NO WIFI** / **NO SIMHUB** (no more `NO LINK`); 0.7.0 added
**multiple displays**, **corner navigation**, the
**rev-limiter page**, **per-display orientation**, the **previous-lap delta bar**,
and the **daily alarm** on the CYD. The firmware and plugin must be updated
together: the 0.7.0+ plugin sends `rev`, `delta` and `orientation`, which older
firmware returns 404 for (flags and timing keep working, but the new pages stay
empty).

Verify a download with:

```bash
sha256sum -c SHA256SUMS
```

The images were built with the placeholder values from
`firmware/secrets.yaml.example`. Build your own if you want a different fallback
AP or OTA password.
