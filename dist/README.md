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
| `CydFlags.dll` | SimHub plugin 0.7.0, copy next to `SimHubWPF.exe` |

A factory flash erases the saved WiFi credentials; an OTA update does not.

0.7.0 adds **multiple displays**, **corner navigation**, the **rev-limiter
page**, **per-display orientation**, the **previous-lap delta bar**, and the
**daily alarm** on the CYD. The firmware and plugin must be updated together:
the 0.7.0 plugin sends `rev`, `delta` and `orientation`, which older firmware
returns 404 for (flags and timing keep working, but the new pages stay empty).

Verify a download with:

```bash
sha256sum -c SHA256SUMS
```

The images were built with the placeholder values from
`firmware/secrets.yaml.example`. Build your own if you want a different fallback
AP or OTA password.
