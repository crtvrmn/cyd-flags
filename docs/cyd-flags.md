# CYD Flags — flags, clock and relative timing on a Cheap Yellow Display

A two-part project that puts the current flag, a clock and a small relative
timing tower from your sim racing game on a 2.8" **Cheap Yellow Display**
(ESP32-2432S028R):

| part | what it is |
|------|------------|
| [`firmware/cyd-flags.yaml`](../firmware/cyd-flags.yaml) | ESPHome firmware: draws the flag/clock/timing screens and exposes a tiny HTTP API |
| [`src/CydFlags/`](../src/CydFlags/) | SimHub plugin: reads flags and relative opponents, POSTs changes to the display |

Outside a race session (menus, practice, qualifying) the display is a clock.
In a real race session it shows the car ahead, your car and the car behind with
position, driver name, gap and lap time — plus a small clock in the top-left.
The header says **LAST** or **BEST** (selectable in the plugin) and turns into
**LAST PIT** while you are in the pit lane.
When a flag is active it still takes over the whole screen. There is no MQTT
broker, no Home Assistant and no PC-side Python bridge. The display runs a web
server; SimHub talks to it directly over your LAN.

Flag changes are **coalesced**: the panel redraws on a fixed 300 ms cadence
instead of once per HTTP request, so a burst of yellow/blue/white changes cannot
make the display queue up and lag behind. You always see the newest state, and
intermediate flags that lasted only a few hundred milliseconds are skipped.

```
game ──telemetry──► SimHub ──► CydFlags plugin ──HTTP POST──► ESP32 CYD
                                   (only on change + heartbeat)
```

---

## Hardware

* **ESP32-2432S028R** "Cheap Yellow Display" — 2.8" 320x240 ILI9341, ESP32-WROOM-32.
  This is the original board with a single micro-USB port.
* Power and data over USB. No wiring is needed for the display: it is already
  connected to the ESP32 on the board.
* The panel is used in **portrait** (240x320), so the USB port is on the
  short side and the cable does not stick out sideways.
* The resistive touch controller (XPT2046) is on CLK GPIO25, MOSI GPIO32,
  MISO GPIO39 and CS GPIO33; it is polled separately from the display SPI bus.

If you have one of the newer dual-USB boards, the panel controller may differ.
The firmware has a dedicated model for each variant, see
[Panel variants](#panel-variants).

---

## 1. Flash the firmware

### Easiest: prebuilt binary (no ESPHome install)

The repository contains ready-to-flash **portrait** images. Pick the file for
the panel controller of your board:

| file | board |
|------|-------|
| [`dist/cyd-flags-portrait-ili9341.factory.bin`](../dist/cyd-flags-portrait-ili9341.factory.bin) | original single-USB CYD, ILI9341 |
| [`dist/cyd-flags-portrait-7789.factory.bin`](../dist/cyd-flags-portrait-7789.factory.bin) | dual-USB CYD with ST7789V (most common newer variant) |
| [`dist/cyd-flags-portrait-9342.factory.bin`](../dist/cyd-flags-portrait-9342.factory.bin) | dual-USB CYD with ILI9342 |

Each `.factory.bin` is for the first USB flash; the matching `.ota.bin` is for
later updates. If the image is mirrored, shows wrong colours or has a stripe,
try one of the other two files — the panel controller is the only difference.

Open **https://web.esphome.io** in Chrome/Edge, connect the CYD over USB, click
**Install**, and choose the `.factory.bin`. The images include `improv_serial`,
so ESPHome Web normally asks for your WiFi SSID and password immediately after
flashing — no fallback AP and no ESPHome install needed.

> **Note:** a factory flash erases the saved WiFi credentials. An OTA update
> does **not**: it only replaces the application partition, while the WiFi
> credentials stay in NVS. So once the display is on your network, later
> updates should use the matching `.ota.bin` via OTA, not the `.factory.bin`
> over USB.

If the Improv prompt does not appear, the firmware still opens the
**CYD Flags fallback** AP (`cyd-fallback`) for captive-portal WiFi setup.
A German step-by-step guide is in [ANLEITUNG.md](../ANLEITUNG.md).

### Build it yourself

#### Install ESPHome

```bash
python3 -m pip install --user esphome
# or: pipx install esphome
esphome version
```

The config was validated and compiled with **ESPHome 2026.10.2** (ESP-IDF
5.5.6, Arduino-style framework setting). A current ESPHome release is fine.

#### Configure WiFi

```bash
cd firmware
cp secrets.yaml.example secrets.yaml
$EDITOR secrets.yaml
```

`secrets.yaml` is deliberately git-ignored; never commit real credentials.
The display falls back to its own access point named **CYD Flags fallback** if
it cannot join your WiFi.

#### First flash over USB

Connect the CYD with a **data-capable** USB cable (many cables are power-only).

```bash
cd firmware
esphome run cyd-flags.yaml
```

Pick the serial port when prompted. The first build downloads the ESP32
toolchain and takes several minutes. After that, updates can go over the air:

```bash
esphome run cyd-flags.yaml --device cyd-flags.local
```

> **Linux note:** if `~/.cache` is read-only (some sandboxes), point the build
> caches into the repository:
>
> ```bash
> XDG_CACHE_HOME=$PWD/.cache \
> PLATFORMIO_CORE_DIR=$PWD/.platformio \
> ESPHOME_DATA_DIR=$PWD/.esphome-data \
> esphome run cyd-flags.yaml
> ```

### Find the display

The firmware advertises itself as `cyd-flags.local` over mDNS. On networks
where mDNS is unreliable (often Windows), give the CYD a DHCP reservation in
your router and use its IP address instead.

Open `http://cyd-flags.local/` in a browser — you should see the ESPHome web
UI and the flag entities.

### Panel variants

Edit the substitutions at the top of `firmware/cyd-flags.yaml`:

| board | setting |
|-------|---------|
| original CYD, micro-USB, ILI9341 | `display_model: ESP32-2432S028` (default) |
| dual-USB CYD with ST7789V | `display_model: ESP32-2432S028-7789` |
| dual-USB CYD with ILI9342 | `display_model: ESP32-2432S028-9342` |

`display_rotation: "0"` is portrait (default). Use `"90"` for landscape, or
`"270"` if the portrait image is upside down. If red and blue are swapped, add
`color_order: RGB` to the `display:` block (or `BGR` if the other way around).

---

## 2. Install the SimHub plugin

### Option A: use the prebuilt DLL

Copy [`dist/CydFlags.dll`](../dist/CydFlags.dll) into the SimHub installation
folder (next to `SimHubWPF.exe`, usually `C:\Program Files (x86)\SimHub`), then
restart SimHub.

If Windows marks the downloaded DLL as blocked: right-click → **Properties** →
**Unblock**.

### Option B: build from source

Open [`CydFlags.sln`](../CydFlags.sln) in Visual Studio 2022 and build the
**CydFlags** project (Debug or Release, Any CPU). The post-build step copies
the DLL into SimHub when `SIMHUB_INSTALL_PATH` / `SimHubPath` points at your
SimHub folder.

The project targets **.NET Framework 4.8** and references `GameReaderCommon.dll`,
`SimHub.Plugins.dll` and `SimHub.Logging.dll` from the SimHub folder. The
settings panel is built in code (no XAML), so a plain C# compiler is enough.

### Configure it

1. SimHub → **Settings → Plugins → CYD Flags**.
2. Set **Host or IP** to `cyd-flags.local` or the display's IP address.
3. Leave **Port** at `80`, tick **Enable CYD Flags**.
4. Press **Test connection**. The status line should turn green and the CYD
   should show **NO FLAG** (or the current flag, if a game is running).

That is all: start a game and the flag appears.

### Pages, WiFi and flag tests

The same settings page has three more groups:

- **Pages on the display** — tick **Flags**, **Clock**, **Relative tower** and
  **Race logic**. The plugin sends this list to the `features` entity. Disabled
  pages disappear from the touch cycle; with nothing selected the CYD shows
  **NO PAGE**. Flags keep priority over all pages.
- **WiFi on the display** — enter the SSID and password and press **Save WiFi
  on display and connect**. The plugin writes `wifi_ssid`/`wifi_password` and
  presses the `wifi_connect` button, which calls ESPHome's `wifi.configure` with
  `save: true`. Credentials are stored in the ESP32's NVS, so they survive OTA
  updates and power cycles. A full USB/**factory** flash still erases NVS.
  The password is never written to the plugin settings file.
- **Test the flags** — choose a flag and press **Show for 10 seconds**. This
  overrides the game for ten seconds (the panel brightens even if idle dimming
  is active), then normal SimHub data resumes. **Stop test** ends it early.

**Open display web UI** launches the display's ESPHome page in the default
browser, so you do not have to type the IP by hand.

### Backlight and idle dimming

The panel's backlight can be controlled from the same settings page:

- **Set display brightness from SimHub** pushes the slider value when the
  settings are applied.
- **Turn backlight off (0%) while no game is running** is optional. When
  enabled, the plugin sends `0%` as soon as SimHub reports that no game process
  is running, and restores the slider value when a game starts. While the panel
  is off it cannot show its IP address, so finish the network setup first and
  only then enable this option.
- **Push brightness now** forces one immediate push.

The `brightness` entity is a normal ESPHome number: `0` turns the backlight
off, `100` is full brightness.

---

## What the display shows

The panel is portrait (240×320). There are four screen types:

1. **Race timing** — only during a real race session. Three rows (ahead, you,
   behind), each with position, driver name, gap and lap time. A small clock
   sits in the top-left; no date. The header above the rows says **LAST** or
   **BEST** (plugin setting) and **PIT** is appended while you are in the pit
   lane. The gap is green while the car ahead is being caught (and red while it
   pulls away); for the car behind the colors are reversed. The lap time gets a
   background: **purple** for the fastest lap, **red** when it is faster than
   your own lap, **green** when it is slower. Your own row has no gap (0.00 is
   meaningless) and can only be purple, never red/green against itself.
2. **Race logic** — a third race page with your current lap time, a live delta
   to your personal best, and the three sector times. Each sector card is
   **green** when that sector was faster than your best lap's sector and
   **red** when it was slower. A sector that has not been completed yet shows
   `--.---` on a neutral background.
3. **Flag** — a flag reported by the game takes over the whole screen, exactly
   like before.
4. **Clock** — menus, practice, qualifying, and the no-link fallback show the
   big NTP clock and the date.

**Touch:** during a race a tap anywhere cycles **tower → race logic → clock**;
the next tap starts over at the tower. Taps outside a race are ignored, and an
active flag still has priority over all pages. The touch controller (XPT2046)
is polled on its own SPI pins; only tap detection is used, so the full panel
works as one button and no precise calibration is needed. The page selection is
reset to the tower when the race session ends.

```
┌────────────────────────┐
│ 16:42  LAST   192.168.1.42│ small clock + header + IP hint
│ ┌──┐ Fabio Scherer 0.45│  ahead
│ │ 6│ 1:47.383           │
│ └──┘                     │
│ ┌──┐ YOU                │  player row highlighted
│ │ 7│ 1:48.102           │
│ └──┘                     │
│ ┌──┐ Marcel Kuehnel 9.51│ behind
│ │10│ 1:54.765           │
│ └──┘                     │
└────────────────────────┘
```

The IP address is only a boot hint: it appears for 15 seconds after WiFi gets a
lease (bottom of the clock screen, top-right during a race), then disappears. There is no permanent “CYD FLAGS” or
“SIMHUB FLAGS” label anymore.

| flag | background | text | subtitle |
|------|-----------|------|----------|
| Green | green | GREEN | TRACK CLEAR |
| Yellow | yellow | YELLOW | CAUTION |
| Blue | blue | BLUE | LET FASTER CARS BY |
| White | white | WHITE | FINAL LAP |
| Checkered | black/white checkerboard | FINISH | CHECKERED FLAG |
| Red | red | RED | SESSION STOPPED |
| Black | near black | BLACK | PENALTY - PIT |
| Orange | orange | ORANGE | MEATBALL - DAMAGE |
| no race / idle | dark | real clock (HH:MM, NTP) | date |
| no heartbeat for 5 s | dark | real clock (amber) | NO LINK |

If several flags are reported at once, the most important one wins:

**red → black → checkered → orange → yellow → blue → white → green**

When no flag is active the hero area becomes a clock. It uses ESPHome's SNTP
component (`timezone: Europe/Berlin`); until the first NTP sync it shows
`--:--` / `CLOCK SYNC`. Outside a race session this is the main screen. During
a race the clock shrinks to the top-left corner and the timing tower fills the
panel. When the SimHub link is down the big clock stays visible with **NO LINK**
as the subtitle.

The plugin reads SimHub's `Flag_Red`, `Flag_Black`, `Flag_Checkered`,
`Flag_Orange`, `Flag_Yellow`, `Flag_Blue`, `Flag_White`, `Flag_Green`
booleans, and falls back to `Flag_Name` if a game only fills the string.
For the race tower it reads `SessionTypeName` (only `Race` counts; practice,
qualifying and menus stay on the clock) and the
`OpponentsAheadOnTrack` / `OpponentsBehindOnTrack` lists, formatting position,
gap and lap time on the PC. The plugin setting **Race tower: show best lap
instead of last lap** switches the values and the header between LAST and BEST;
while you are in the pit lane the header appends **PIT**. Driver names are
folded to plain ASCII (`René` -> `Rene`, `Müller` -> `Mueller`, `MÜLLER` ->
`MUELLER`) because the CYD font only contains the Latin glyphs shown on screen.

**Start lights and pit flag:** SimHub's `StatusDataBase` does not expose a start
light / countdown property, and it has no dedicated pit-lane flag. The **PIT**
in the header is therefore a hint derived from `IsInPitLane`, not a real race
control flag. RaceRoom's raw shared memory does contain `start_lights` and
`start_phase`, but SimHub does not map them into `StatusDataBase`; reading that
memory directly would be a separate, game-specific feature.

---

## HTTP API

The firmware's ESPHome web server accepts one POST per entity. This is the
whole protocol:

```bash
# flag: NONE, GREEN, YELLOW, BLUE, WHITE, CHECKERED, RED, BLACK, ORANGE
curl -X POST 'http://cyd-flags.local/select/flag/set?option=YELLOW'

# game name, URL-encoded, max 64 chars
curl -X POST 'http://cyd-flags.local/text/game/set?value=Assetto%20Corsa%20Competizione'

# race mode: RACE shows the timing tower, CLOCK shows the big clock
curl -X POST 'http://cyd-flags.local/text/race/set?value=RACE'

# header above the tower: LAST, BEST, LAST PIT or BEST PIT
curl -X POST 'http://cyd-flags.local/text/lap_label/set?value=LAST'

# relative timing rows, max 255 chars. Three rows separated by ';', six
# fields per row separated by '~':
# position~name~gap~gapcolor~lap~lapcolor
# Colors: -=none, G=green, R=red, P=purple (fastest lap).
curl -X POST 'http://cyd-flags.local/text/timing/set?value=6~Fabio~0.45~G~1:47.383~P%3B7~YOU~~-~1:48.102~-%3B10~Marcel~9.51~R~1:54.765~R'

# race-logic page: current lap, delta to personal best, three sectors
# lap~lapcolor~delta~deltacolor~s1~s1color~s2~s2color~s3~s3color
# Sector colors compare with your best lap: G faster, R slower.
curl -X POST 'http://cyd-flags.local/text/logic/set?value=1:23.456~-~-0.234~G~31.234~G~35.100~R~--.---~-'

# pages: comma-separated flags,clock,tower,logic; "none" hides all pages
curl -X POST 'http://cyd-flags.local/text/features/set?value=flags,clock,tower,logic'

# WiFi provisioning: write credentials, then press the button. save:true puts
# them in NVS; they survive OTA/reboot but not a full factory flash.
curl -X POST 'http://cyd-flags.local/text/wifi_ssid/set?value=MyNetwork'
curl -X POST 'http://cyd-flags.local/text/wifi_password/set?value=secret'
curl -X POST 'http://cyd-flags.local/button/wifi_connect/press'

# heartbeat: any number; the display considers the link alive for 5 s
curl -X POST 'http://cyd-flags.local/number/heartbeat/set?value=42'

# backlight, 0..100 %
curl -X POST 'http://cyd-flags.local/number/brightness/set?value=80'
```

Read state with `GET /select/flag`, `GET /text/game`, etc. ESPHome only accepts
**POST** for the `set` action.

The entity names (`flag`, `game`, `race`, `timing`, `logic`, `lap_label`,
`features`, `wifi_ssid`, `wifi_password`, `wifi_connect`, `heartbeat`,
`brightness`) are matched **exactly** by ESPHome's web server. If you rename an
entity, update the plugin (see `DeviceClient.cs`) and these URLs too.

---

## Troubleshooting

| symptom | cause / fix |
|---------|-------------|
| **NO LINK** on the display | PC cannot reach the CYD. Check host/IP in the plugin, ping the display, try the IP instead of `.local`. The plugin backs off briefly after failures but keeps retrying. |
| Plugin status says **404** | Entity name mismatch. Only rename entities if you also change `DeviceClient.cs`; the default names are `flag`, `game`, `race`, `timing`, `logic`, `lap_label`, `features`, `wifi_ssid`, `wifi_password`, `wifi_connect`, `heartbeat`, `brightness`. |
| **Page list / WiFi save does nothing** | The display is running an older firmware before 0.6.0. Flash the current factory/OTA image; the plugin then finds the `features` and `wifi_*` entities. |
| **WiFi is gone after an update** | A **factory** USB flash erases NVS. Use the `.ota.bin` for wireless updates, or enter the credentials again with **Save WiFi on display**. |
| Race-logic sectors stay `--.---` | The game does not expose current sector times for the player. The page still shows lap and delta; sectors fill in as the game reports them. |
| Display stays **blank** | Wrong `display_model` for your board, or backlight not on. Check the USB cable is data-capable and the board variant table above. |
| Image **upside down** | `display_rotation: "270"`. |
| **Red/blue swapped** | `color_order: RGB` (or `BGR`) in the display block. |
| Plugin does not appear in SimHub | DLL not in the SimHub folder, blocked by Windows, or SimHub not restarted. Build for **.NET Framework 4.8**, Any CPU. |
| WiFi not connecting | `secrets.yaml` not filled in, 5 GHz-only network (ESP32 is 2.4 GHz only), or weak signal. Connect to the **CYD Flags fallback** AP and open `http://192.168.4.1/`. |
| Flags never change | The game reader may not populate flag properties. Check SimHub's **Game Data** / properties for `Flag_Yellow` etc.; the plugin falls back to `Flag_Name` only if all booleans are false. |
| Race tower shows `---` | The game/session does not expose relative opponents. The tower falls back to placeholders; the clock still works. Check `SessionTypeName` contains `Race` and that SimHub's opponent list is populated. |

Build/test notes for developers:

```bash
# pure logic tests (flag priority, name parsing, option strings)
dotnet run --project tests

# rebuild the plugin on Linux (Roslyn + net48 refs + SimHub SDK DLLs)
bash tools/build/build-cydflags.sh
```

---

## Repository layout

```
firmware/cyd-flags.yaml          ESPHome firmware (display + REST entities)
firmware/secrets.yaml.example    WiFi / OTA placeholders
firmware/fonts/                  DejaVu Sans Condensed Bold + license
src/CydFlags/CydFlagsPlugin.cs   SimHub plugin lifecycle + worker
src/CydFlags/FlagReader.cs       StatusDataBase flag reads (reflection fallback)
src/CydFlags/FlagModel.cs        priority selection / name parsing (tested)
src/CydFlags/TimingBuilder.cs    relative tower payload
src/CydFlags/RaceLogicBuilder.cs lap / sector / delta payload
src/CydFlags/DeviceClient.cs     tiny POST client for ESPHome's web server
src/CydFlags/UI/                 settings panel (code-only WPF)
tests/                           pure C# tests for FlagModel
dist/CydFlags.dll                prebuilt plugin
docs/cyd-flags.md                this file
```

## Credits / license

The display font is **DejaVu Sans Condensed Bold**, distributed under the
Bitstream Vera license; see `firmware/fonts/LICENSE-DejaVu.txt`. The code in
this repository is released under the MIT license; see `LICENSE`.
