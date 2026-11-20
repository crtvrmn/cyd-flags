# CYD Flags

Flags, a clock, a relative race tower, a race-logic page, a rev-limiter page
and a daily alarm clock on one or more **Cheap Yellow Displays**
(ESP32-2432S028), driven directly by **SimHub**.

No Home Assistant, no MQTT broker and no PC-side Python bridge: each ESP32 runs
an ESPHome web server, and a small SimHub plugin pushes the current state to
every configured display over your LAN. One display can be offline without
delaying the others.

**German step-by-step guide:** [ANLEITUNG.md](ANLEITUNG.md)  
**Technical reference:** [docs/cyd-flags.md](docs/cyd-flags.md)

---

## What it shows

| situation | screen |
|-----------|--------|
| race session | relative tower: car ahead, you, car behind — position, name, gap and lap time; optional live delta-to-previous-lap bar |
| race session, bottom-right corner | next page: race logic / rev limiter / clock; bottom-left goes back |
| race logic | current lap, sector times (green/red vs. your best), delta to best |
| rev limiter | large gear, speed, RPM bar that blinks red at the redline; also in practice/qualifying |
| menus / practice / qualifying | large clock and date, with a small alarm shortcut top-left |
| alarm time reached | full-screen flashing ALARM; tap anywhere to stop |
| any flag | full-screen flag (red, yellow, blue, white, checkered, black, orange…) |
| no WiFi association | clock with `NO WIFI` (or `WIFI: SETUP AP` in the fallback AP) |
| WiFi up, no SimHub heartbeat for 5 s | clock with `NO SIMHUB` |

Flags always take priority over the timing pages. The header of the tower shows
**LAST** or **BEST** (selectable per display in the plugin), with a **PIT**
suffix while you are in the pit lane. The IP address is shown for the first
15 seconds after boot. Every display can be mounted **portrait or landscape**;
the plugin rotates it at runtime, so one firmware image works for both.

The display redraws on a fixed 300 ms cadence (the rev-limiter page uses
50 ms with partial updates), so rapid flag changes are coalesced instead of building up a laggy queue. Page selection, backlight and
WiFi are configured per display; the race data itself is shared.

```
                                    ┌──HTTP POST──► ESP32 CYD #1
game ──telemetry──► SimHub ──► CydFlags plugin ──┼──HTTP POST──► ESP32 CYD #2
                                    └──HTTP POST──► ESP32 CYD #3
```

---

## Quick start

1. **Flash the firmware.** Use Chrome/Edge and <https://web.esphome.io>.
   Pick the prebuilt file for your panel (see the table below), flash it, and
   enter your 2.4 GHz WiFi credentials when ESPHome Web asks for them.
   - A **factory** flash erases the previously saved WiFi credentials.
   - An **OTA** update keeps them.
   - The plugin also has **Save WiFi on display**, which stores credentials in
     NVS and survives OTA updates.
2. **Install the plugin.** Copy [`dist/CydFlags.dll`](dist/CydFlags.dll) next to
   `SimHubWPF.exe` (usually `C:\Program Files (x86)\SimHub`), then restart
   SimHub and enable **CYD Flags** under **Plugins**.
3. **Connect.** Enter the display's hostname (`cyd-flags.local`) or IP address
   in the plugin settings and press **Test connection**. For more displays use
   **Add device** and give each one its own host or IP.
4. **Drive.** The plugin sends flag, game, race mode, timing, race logic,
   lap-label and heartbeat updates to every enabled display automatically.

The German guide has screenshots-free, click-by-click instructions, including
troubleshooting: [ANLEITUNG.md](ANLEITUNG.md).

From the plugin you can also:

- **Choose pages per display** — flags only, clock only, tower, race logic,
  rev limiter, or any combination. Disabled pages disappear from that
  display's corner navigation and the ESP stores the choice. By default the
  tower, race logic and delta also work in practice/qualifying; a global
  checkbox restricts them to races and keeps the big clock elsewhere.
- **Choose the mounting orientation per display** — portrait 240×320 or
  landscape 320×240; applied at runtime, no separate firmware image.
- **Show a live delta bar** to the previous lap on the race tower: green to the
  right when you are faster, orange to the left when you are slower.
- **Set a daily alarm** on the display itself: tap the small clock icon
  top-left on the idle clock screen, then use the four quadrants
  (TL +1 h, TR +5 min, BL off/cancel, BR save/arm). It survives reboots/OTA
  and can play a best-effort tone on GPIO26.
- **Open the selected display's web UI** with one click.
- **Store WiFi on the selected display** — written to the ESP's NVS with
  `wifi.configure save: true`, so it survives OTA updates and reboots. A full
  factory flash still erases it.
- **Test every flag for 10 seconds on all displays** without starting a game.

---

## Prebuilt firmware

| file | board |
|------|-------|
| [`dist/cyd-flags-portrait-ili9341.factory.bin`](dist/cyd-flags-portrait-ili9341.factory.bin) | original single-USB CYD, ILI9341 |
| [`dist/cyd-flags-portrait-7789.factory.bin`](dist/cyd-flags-portrait-7789.factory.bin) | dual-USB CYD with ST7789V (most common newer variant) |
| [`dist/cyd-flags-portrait-9342.factory.bin`](dist/cyd-flags-portrait-9342.factory.bin) | dual-USB CYD with ILI9342 |

Each `.factory.bin` is for the first USB flash; the matching `.ota.bin` is for
later wireless updates. If the image is mirrored, has a stripe or shows wrong
colours, flash the next panel variant — the panel controller is the only
difference. SHA-256 checksums are in [`dist/SHA256SUMS`](dist/SHA256SUMS).

The prebuilt images were built with the placeholder values from
`firmware/secrets.yaml.example`; that only matters for the fallback AP and OTA
password until you build your own. They boot in portrait; the plugin can rotate
each display to landscape at runtime.

---

## Hardware

* **ESP32-2432S028R** “Cheap Yellow Display”, 2.8" 320×240, ESP32-WROOM-32.
* The panel boots in **portrait** (240×320); the plugin can rotate it to
  **landscape** (320×240) per display at runtime.
* Display SPI: SCK GPIO14, MOSI GPIO13, CS GPIO15, DC GPIO2, backlight GPIO21.
* Touch (XPT2046): CLK GPIO25, MOSI GPIO32, MISO GPIO39, CS GPIO33.
* No wiring is needed; everything is already connected on the board.

The firmware uses ESPHome's `mipi_spi` display platform with a dedicated CYD
model. See [Panel variants](docs/cyd-flags.md#panel-variants) for `ILI9341`,
`ST7789V` and `ILI9342`.

---

## Repository layout

```
firmware/cyd-flags.yaml          ESPHome firmware: display + REST API
firmware/secrets.yaml.example    WiFi / fallback AP / OTA placeholders
firmware/fonts/                  DejaVu Sans Condensed Bold + license
src/CydFlags/                    SimHub plugin (C#, .NET Framework 4.8)
  CydFlagsPlugin.cs              lifecycle, per-display worker, HTTP push
  CydFlagsSettings.cs            global + per-display settings and migration
  FlagReader.cs                  StatusDataBase flag reads
  FlagModel.cs                   flag priority + name parsing (unit-tested)
  TimingBuilder.cs               relative tower payload
  RaceLogicBuilder.cs            lap/sector/delta payload
  RevBuilder.cs                  gear/speed/RPM payload
  DeltaTracker.cs                previous-lap delta by track position
  DeviceClient.cs                POST client for the ESPHome web server
  UI/                            code-built WPF settings page
tests/                           pure C# tests for FlagModel + DeltaTracker
docs/cyd-flags.md                full technical reference
dist/                            prebuilt firmware + plugin DLL
ANLEITUNG.md                     German beginner guide
```

---

## Building the plugin

Requirements:

* Visual Studio 2022 with **.NET Framework 4.8** targeting pack
* SimHub installed, or `SIMHUB_INSTALL_PATH` pointing at the folder that
  contains `SimHubWPF.exe`

Open [`CydFlags.sln`](CydFlags.sln), set `SIMHUB_INSTALL_PATH` if needed and
build. The post-build step copies `CydFlags.dll` (and the PDB) to the SimHub
folder automatically.

On Linux there is a small helper that compiles with Roslyn + the .NET Framework
4.8 reference assemblies. Those inputs are not redistributed, so point it at
your local copies:

```bash
# These are local, non-redistributed tools; point the script at yours.
DOTNET=/path/to/dotnet \
CSC=/path/to/sdk/.../Roslyn/bincore/csc.dll \
REFASM=/path/to/net48/reference/assemblies \
SIMHUB_REF=/path/to/SimHub \
bash tools/build/build-cydflags.sh
```

The plugin references `GameReaderCommon.dll`, `SimHub.Plugins.dll`,
`SimHub.Logging.dll` and `log4net.dll` from the SimHub installation. Those DLLs
are **not** part of this repository.

## Building the firmware

Install ESPHome 2026.10.2 or newer, then:

```bash
cd firmware
cp secrets.yaml.example secrets.yaml   # fill in WiFi / OTA values
esphome config cyd-flags.yaml
esphome compile cyd-flags.yaml
```

The YAML exposes substitutions for the panel and orientation:

```yaml
substitutions:
  display_model: ESP32-2432S028      # or ESP32-2432S028-7789 / -9342
  display_rotation: "0"              # "90" = landscape, "270" = upside down
  backlight_pin: GPIO21
```

The build output lands in `.esphome/build/cyd-flags/`; use
`firmware.factory.bin` for USB and `firmware.ota.bin` for wireless updates.

## Tests

```bash
dotnet run --project tests
```

This exercises `FlagModel` (flag priority, `Flag_Name` parsing, option
strings) and `DeltaTracker` (previous-lap interpolation/reset cases). It needs
the .NET 8 SDK and no SimHub installation.

---

## HTTP API

The firmware exposes standard ESPHome template entities. The plugin uses:

```text
POST /select/flag/set?option=YELLOW
POST /text/game/set?value=RaceRoom
POST /text/race/set?value=RACE          # or CLOCK
POST /text/timing/set?value=...         # position~name~gap~gapcolor~lap~lapcolor;...
POST /text/logic/set?value=...          # lap~lapcolor~delta~deltacolor~s1~...~s3color
POST /text/lap_label/set?value=LAST     # LAST / BEST / LAST PIT / BEST PIT
POST /text/rev/set?value=4~123~7800~8500~8000~92~0
POST /text/delta/set?value=-0.421       # signed seconds, empty hides
POST /text/orientation/set?value=portrait  # or landscape
POST /text/features/set?value=flags,clock,tower,logic,rev  # or none
POST /number/heartbeat/set?value=123
POST /number/brightness/set?value=90
POST /text/wifi_ssid/set?value=MyNetwork
POST /text/wifi_password/set?value=secret
POST /button/wifi_connect/press
```

The plugin computes flag, game, race mode, timing and race-logic data once, then
POSTs the applicable parts to each enabled display. Timing is skipped for a
display whose tower page is disabled and race logic for one without the logic
page. Heartbeats and brightness are sent per display, so a disconnected panel
only backs off itself. The `features`, `wifi_*`, `rev`, `delta` and `orientation` endpoints belong
to firmware 0.7.0 or newer.

A heartbeat younger than 5 seconds marks the display as linked. Flags sent
through the web UI alone disappear after 5 seconds; the SimHub plugin sends a
heartbeat every second.

---

## Limitations

* **Start lights / countdown are not available through SimHub.** RaceRoom has
  the raw data in its shared memory, but SimHub does not map it to
  `StatusDataBase`; this project does not read the game's shared memory
  directly.
* **PIT** is derived from `IsInPitLane`; it is a pit-lane hint, not an official
  race-control pit flag.
* Race-tower and race-logic data depend on the game/SimHub reader. If a game
  does not populate opponents or sector times, the display falls back to
  placeholders; the clock and flags still work.
* The plugin was built and unit-tested on Linux. The flag/clock/touch path has
  been tested on a real CYD; the race pages need a real SimHub session.

---

## Credits / license

The display font is **DejaVu Sans Condensed Bold**, distributed under the
Bitstream Vera license; see
[`firmware/fonts/LICENSE-DejaVu.txt`](firmware/fonts/LICENSE-DejaVu.txt).

The code in this repository is released under the MIT license; see
[`LICENSE`](LICENSE).
