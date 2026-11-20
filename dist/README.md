# Prebuilt artifacts

All images are **portrait** (240×320) and built from `firmware/cyd-flags.yaml`
with ESPHome 2026.10.2 / ESP-IDF 5.5.6.

| file | use |
|------|-----|
| `cyd-flags-portrait-ili9341.factory.bin` | first USB flash, original single-USB CYD (ILI9341) |
| `cyd-flags-portrait-7789.factory.bin` | first USB flash, dual-USB CYD (ST7789V) |
| `cyd-flags-portrait-9342.factory.bin` | first USB flash, dual-USB CYD (ILI9342) |
| `*.ota.bin` | wireless update of the matching panel variant |
| `CydFlags.dll` | SimHub plugin 0.6.0, copy next to `SimHubWPF.exe` |

A factory flash erases the saved WiFi credentials; an OTA update does not.

0.6.0 adds the **features**, **wifi_ssid**, **wifi_password** and
**wifi_connect** entities used by the plugin's page selection and WiFi
provisioning buttons. The plugin and firmware should be updated together.

Verify a download with:

```bash
sha256sum -c SHA256SUMS
```

The images were built with the placeholder values from
`firmware/secrets.yaml.example`. Build your own if you want a different fallback
AP or OTA password.
