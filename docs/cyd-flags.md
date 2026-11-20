# CYD Flags — flags, clock and relative timing on Cheap Yellow Displays

A two-part project that puts the current flag, a clock, a small relative
timing tower, race logic, a rev limiter, SABT belt-tensioner settings and a daily alarm from your sim racing
game on one or more 2.8" **Cheap Yellow Displays** (ESP32-2432S028R):

| part | what it is |
|------|------------|
| [`firmware/cyd-flags.yaml`](../firmware/cyd-flags.yaml) | ESPHome firmware: draws the flag/clock/timing screens, receives fast UDP updates and exposes an HTTP configuration API |
| [`src/CydFlags/`](../src/CydFlags/) | SimHub plugin: reads flags and relative opponents, sends realtime data over UDP and configuration over HTTP |

Outside a race session (menus, practice, qualifying) the display is a clock.
In a real race session it shows the car ahead, your car and the car behind with
position, driver name, lap time and a trend disc (blue neutral, green +, red −) — plus a small clock in the top-left.
The header says **LAST** or **BEST** (selectable per display in the plugin) and
turns into **LAST PIT** while you are in the pit lane.
When a flag is active it still takes over the whole screen. At boot the panel
shows a short `CYD FLAGS` splash with the firmware version for eight seconds,
so you can confirm which image is running. There is no MQTT broker, no Home
Assistant and no PC-side Python bridge. Each display listens
for realtime updates on **UDP port 18512** and runs a small HTTP server on
port 80 for configuration; SimHub talks to both directly over your LAN.

All displays receive the same race data, but pages, backlight and WiFi are
configured per display. A panel that is switched off, unplugged or on the wrong
IP only backs off itself: the plugin keeps updating the other panels.

Realtime data goes over **UDP**, not HTTP. The plugin shows the display's
received-packet counter in its settings (`UDP: OK`, `UDP: blocked`,
`UDP: unknown`) so a firewall or wrong-IP problem is visible without guessing. The display itself shows a small `UDP BLOCKED` badge when it is linked over HTTP but has not received a UDP packet for 10 s. The race tower is built at 20 Hz
and drawn at 12.5 Hz (80 ms), the rev page is built at 50 Hz and drawn at 33 Hz
(30 ms), race logic at 12.5 Hz; the clock and flag screens stay at 300 ms. That is roughly
an order of magnitude less latency than the old HTTP-only path. HTTP is still
used for configuration (pages, orientation, brightness, WiFi) and for manual
testing with `curl`.

```
                                 ┌──UDP 18512 (realtime)──► ESP32 CYD #1
game ──telemetry──► SimHub ──► CydFlags plugin ──┼──UDP 18512──────────────► ESP32 CYD #2
                                 └──HTTP 80 (config)──────► ESP32 CYD #3
                                    per display, no cloud
```

---

## Hardware

* **ESP32-2432S028R** "Cheap Yellow Display" — 2.8" 320x240 ILI9341, ESP32-WROOM-32.
  This is the original board with a single micro-USB port.
* Power and data over USB. No wiring is needed for the display: it is already
  connected to the ESP32 on the board.
* The panel boots in **portrait** (240x320); the SimHub plugin can rotate it to
  **landscape** (320x240) at runtime per display, so one image fits either
  mounting.
* The resistive touch controller (XPT2046) is on CLK GPIO25, MOSI GPIO32,
  MISO GPIO39 and CS GPIO33; it is polled separately from the display SPI bus.

If you have one of the newer dual-USB boards, the panel controller may differ.
The firmware has a dedicated model for each variant, see
[Panel variants](#panel-variants).

---

## 1. Flash the firmware

### Easiest: prebuilt binary (no ESPHome install)

The repository contains ready-to-flash images. They boot portrait; select
portrait or landscape per display in the plugin. Pick the file for the panel
controller of your board:

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

`display_rotation: "0"` is the boot default (portrait). The plugin normally
sets portrait/landscape at runtime through the `orientation` entity. If you
build a fixed-orientation image, `"90"` is landscape and `"270"` rotates a
portrait image that is upside down. If red and blue are swapped, add
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
2. The **Displays** group shows every configured panel as a card. A fresh
   installation starts with one entry, **Display 1**. Upgrading from 0.6.0
   migrates the old single-display host, port, pages and brightness into that
   same entry, so the first display keeps working unchanged.
3. Click the card, set **Host or IP** to `cyd-flags.local` or the display's IP,
   and tick **Enable this display**. Ports are handled internally; there is no
   port field to configure.
4. Press **Test connection**. The status line turns green and the selected CYD
   should show **NO FLAG** (or the current flag, if a game is running).
5. Press **Add display** for each additional panel, click its card and give it
   its own host/IP. **Remove selected** deletes the selected entry, but the last
   remaining display cannot be removed.

The status line and the SimHub properties follow the selected card, so **Last
reply** and **error** describe that panel. The other panels
keep running; an unreachable display only retries its own requests.

### Pages, WiFi and flag tests

The groups below the cards always edit the **selected display**:

- **Pages on this display** — tick **Flags**, **Clock**, **Relative tower**,
  **Race logic**, **Rev limiter** and **SABT**. The plugin sends this list to that
  display's `features` entity. Disabled pages disappear from its corner
  navigation; with nothing selected the CYD shows **NO PAGE**. Flags keep
  priority over all pages. SABT shows the belt-tensioner state from the
  **Simple Active Belt Tensioner** SimHub plugin. Timing data is only sent to displays with the tower
  page enabled, race-logic data only to displays with the logic page enabled,
  so switching a page off also saves network traffic.
- **Only show tower/logic/delta during a race** — global option, unchecked by
  default. Unchecked means the plugin builds and sends the tower, race-logic and
  delta payloads in practice, qualifying and the menus too, so those pages are
  available everywhere. Check it to keep the big clock outside races and only
  enable the three race pages during a race.
- **Display orientation** — **Portrait (240×320)** or **Landscape (320×240)**
  for the selected CYD. The plugin sends `orientation`; the firmware rotates at
  runtime, so the same image works for both mountings. One orientation applies
  to that display, not to individual pages.
- **Race tower: show best lap** — per-display choice between the `LAST` and
  `BEST` lap columns. The plugin computes both variants and sends the selected
  one.
- **Race tower: show delta bar** — optional bar under the tower comparing the
  current lap with the previous lap at the same track position. Green to the
  right means faster, orange to the left means slower; it hides on the first lap
  and after a reset.
- **WiFi on this display** — enter the SSID and password and press **Save WiFi
  on this display and connect**. The plugin writes `wifi_ssid`/`wifi_password`
  to that panel and presses its `wifi_connect` button, which calls ESPHome's
  `wifi.configure` with `save: true`. Credentials are stored in the ESP32's NVS,
  so they survive OTA updates and power cycles. A full USB/**factory** flash
  still erases NVS. The password is never written to the plugin settings file.
- **SABT page** — the checkbox **SABT (belt tensioner settings)** enables the
  page for this display.
- **Test the flags** — choose a flag and press **Show on all displays for 10
  seconds**. This overrides the game for ten seconds on every enabled display
  (the panels brighten even if idle dimming is active), then normal SimHub data
  resumes. **Stop test** ends it early.

**Open display web UI** launches the selected display's ESPHome page in the
default browser, so you do not have to type the IP by hand.

### Backlight and idle dimming

The panel's backlight can be controlled per display from the same settings page:

- **Set display brightness from SimHub** pushes the slider value when the
  settings are applied.
- **Turn backlight off (0%) while no game is running** is optional. When
  enabled, the plugin sends `0%` as soon as SimHub reports that no game process
  is running, and restores the slider value when a game starts. While the panel
  is off it cannot show its IP address, so finish the network setup first and
  only then enable this option.
- **Push brightness now** forces one immediate push to the selected display.

The `brightness` entity is a normal ESPHome number: `0` turns the backlight
off, `100` is full brightness. Heartbeats and brightness are independent per
display, so dimming one panel does not affect another.

---

## What the display shows

The panel can be portrait (240×320) or landscape (320×240); the plugin
rotates it at runtime. There are seven screen types:

1. **Race timing** — only during a real race session. Three rows (ahead, you,
   behind), each with position, driver name, lap time and a trend disc. A small
   clock sits in the top-left; no date. The header above the rows says **LAST**
   or **BEST** (plugin setting) and **PIT** is appended while you are in the pit
   lane. By default the tower is also available in practice and qualifying.
   The disc is **blue** when neutral, a green **+** when you are gaining and a
   red **−** when you are losing (for the car behind, being caught is red and
   pulling away is green). Once green/red it stays visible until the direction
   actually changes; only the first neutral sample is blue. No numeric gap is
   drawn. Flags still take priority over the whole page. The lap time gets a
   background: **purple** for the fastest lap, **red** when it is faster than
   your own lap, **green** when it is slower. Your own row has no disc and can
   only be purple, never red/green against itself.
2. **Race logic** — a third race page with a live delta to your personal best
   and the three sector times; the current-lap time was removed so the cards
   stay readable. Each sector card is
   **green** when that sector was faster than your best lap's sector and
   **red** when it was slower. A sector that has not been completed yet shows
   `--.---` on a neutral background.
3. **Rev limiter** — large gear and speed in km/h only, no bar or RPM text.
   The page polls at 33 Hz (30 ms) and only the changed gear/speed rectangles
   are transferred; the plugin builds it at 50 Hz. The tower/logic pages use
   80 ms, the clock/flag pages 300 ms. Available in races
   and in practice/qualifying.
4. **SABT** — reads the SimHub properties of the Simple Active Belt Tensioner
   plugin (`SABT.IsEnabled`, `SABT.IdleTension`, `SABT.MinimumTension` — with
   `SABT.RestingTension` accepted as an alias — and `SABT.MaximumTension`) and
   shows **ACTIVE**/**OFF** plus the three tension values (`IDLE`, `MIN`,
   `MAX`). Before the first SABT frame the page shows **WAITING** and `--`.
   The page works in the menus too, because it does not need game data.
5. **Flag** — a flag reported by the game takes over the whole screen, exactly
   like before.
6. **Clock** — menus, practice, qualifying, and the no-link fallback show the
   big NTP clock and the date. The top-left alarm icon opens the alarm setup.
7. **Alarm** — a daily alarm fires once at the configured wall-clock time and
   shows a full-screen flashing **ALARM**; any tap stops it for that day.

**Touch:** the panel is split by a 24 px dead cross. The **bottom-left
quadrant** goes to the previous page, the **bottom-right quadrant** to the
next; both wrap and skip disabled pages. The cycle is
**tower → race logic → rev limiter → SABT → clock**. By default all pages also work
outside a race; with the race-pages option checked, practice/qualifying show
only rev limiter and clock. The **top-left quadrant on the idle clock** opens the alarm
setup; during setup the four quadrants are +1 h / +5 min / off / save. During
the alarm overlay any tap stops it.
An active flag still has priority over all pages. The touch controller
(XPT2046) is polled on its own SPI pins; the targets are large quadrants, so no
precise calibration is needed. The X axis is mirror-inverted on the CYD, which
the firmware compensates with `transform.mirror_x: true` so left/right and the
four alarm buttons match the screen. The page selection is reset to the tower when a
new race session starts.

```
┌────────────────────────┐
│ 16:42  LAST   192.168.1.42│ small clock + header + IP hint
│ ┌──┐ Fabio Scherer  +  │  ahead
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
| no WiFi association | dark | real clock | NO WIFI (or WIFI: SETUP AP in the fallback AP) |
| WiFi up, no SimHub heartbeat for 5 s | dark | real clock | NO SIMHUB |

The delta bar under the tower is computed on the PC from `CurrentLap`,
`CurrentLapTime` and `TrackPositionPercent`: the plugin records the previous
lap's time at each track position and interpolates the reference at your
current position. Positive means slower, negative faster. The alarm is a local
CYD feature; it is independent of SimHub, survives OTA/reboots, and its optional
GPIO26 tone is silent on boards without that speaker.

If several flags are reported at once, the most important one wins:

**red → black → checkered → orange → yellow → blue → white → green**

When no flag is active the hero area becomes a clock. It uses ESPHome's SNTP
component (`timezone: Europe/Berlin`); until the first NTP sync it shows
`--:--` / `CLOCK SYNC`. Outside a race session this is the main screen. During
a race the clock shrinks to the top-left corner and the timing tower fills the
panel. Connection problems use a full-screen status instead of the vague
`NO LINK`: **NO WIFI** if the ESP32 has no station association (or
**WIFI: SETUP AP** while the fallback access point is active), and **NO SIMHUB**
if WiFi is up but no heartbeat has arrived for 5 seconds. When both connections
are present the status line is omitted because it would only repeat itself;
the normal clock/date screen is shown.

The plugin reads SimHub's `Flag_Red`, `Flag_Black`, `Flag_Checkered`,
`Flag_Orange`, `Flag_Yellow`, `Flag_Blue`, `Flag_White`, `Flag_Green`
booleans, and falls back to `Flag_Name` if a game only fills the string.
For the race tower it reads `SessionTypeName` (only `Race` counts; practice,
qualifying and menus stay on the clock) and the
`OpponentsAheadOnTrack` / `OpponentsBehindOnTrack` lists, formatting position,
lap time and trend on the PC. The plugin setting **Race tower: show best lap
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

## Fast UDP protocol

The plugin sends one small datagram per value. The first byte is the type, the
rest is UTF-8 text (normally newline-terminated):

| byte | payload | example |
|------|---------|---------|
| `f` | flag option | `fYELLOW` |
| `g` | game name | `gAssetto Corsa Competizione` |
| `t` | tower rows (same `;`/`~` format as `/text/timing`) | `t6~Fabio~+~G~1:47.383~P;...` |
| `l` | race-logic payload | `l1:23.456~-~-0.234~G~31.234~G~35.100~R~--.---~-` |
| `r` | rev payload | `r4~123~7800~8500~8000~92~0` |
| `d` | delta seconds | `d-0.421` |
| `h` | heartbeat counter | `h42` |

The firmware listens on **UDP 18512**; no port forwarding is involved. Windows
normally allows the outbound datagrams. If your firewall blocks UDP, the plugin
falls back to the old HTTP entity calls, but the tower will be noticeably
slower. The HTTP entity names are still available for manual tests.

---

## HTTP API

The firmware's ESPHome web server accepts one POST per entity. The plugin uses
this path for configuration (pages, orientation, brightness, WiFi, test flags);
the realtime values normally arrive over UDP. For manual tests:

```bash
# flag: NONE, GREEN, YELLOW, BLUE, WHITE, CHECKERED, RED, BLACK, ORANGE
curl -X POST 'http://cyd-flags.local/select/flag/set?option=YELLOW'

# game name, URL-encoded, max 64 chars
curl -X POST 'http://cyd-flags.local/text/game/set?value=Assetto%20Corsa%20Competizione'

# race mode: RACE shows the timing tower, CLOCK shows the big clock
curl -X POST 'http://cyd-flags.local/text/race/set?value=RACE'

# pages enabled on this CYD, and its mounting orientation
curl -X POST 'http://cyd-flags.local/text/features/set?value=flags,clock,tower,logic,rev,sabt'
curl -X POST 'http://cyd-flags.local/text/orientation/set?value=portrait'

# rev limiter: gear~speed_kmh~... (only the first two fields are drawn)
curl -X POST 'http://cyd-flags.local/text/rev/set?value=4~123~7800~8500~8000~92~0'

# SABT: enabled~idle~minimum~maximum (resting is accepted as an alias)
curl -X POST 'http://cyd-flags.local/text/sabt/set?value=1~32.5~40.0~75.0'

# previous-lap delta: signed seconds, empty hides the bar
curl -X POST 'http://cyd-flags.local/text/delta/set?value=-0.421'

# header above the tower: LAST, BEST, LAST PIT or BEST PIT
curl -X POST 'http://cyd-flags.local/text/lap_label/set?value=LAST'

# relative timing rows, max 255 chars. Three rows separated by ';', six
# fields per row separated by '~':
# position~name~trend~trendcolor~lap~lapcolor
# trend is +, - or empty (blue neutral); trendcolor B/G/R, lapcolor P/R/G/-.
# Colors: -=none, B=blue, G=green, R=red, P=purple (fastest lap).
curl -X POST 'http://cyd-flags.local/text/timing/set?value=6~Fabio~%2B~G~1:47.383~P%3B7~YOU~~-~1:48.102~-%3B10~Marcel~-~R~1:54.765~R'

# race-logic page: delta to personal best, three sectors
# lap~lapcolor~delta~deltacolor~s1~s1color~s2~s2color~s3~s3color
# (lap is still in the payload for API compatibility; the page no longer draws it)
# Sector colors compare with your best lap: G faster, R slower.
curl -X POST 'http://cyd-flags.local/text/logic/set?value=1:23.456~-~-0.234~G~31.234~G~35.100~R~--.---~-'

# pages: comma-separated flags,clock,tower,logic,rev,sabt; "none" hides all pages
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
`features`, `wifi_ssid`, `wifi_password`, `wifi_connect`, `sabt`, `heartbeat`,
`brightness`) are matched **exactly** by ESPHome's web server. If you rename an
entity, update the plugin (see `DeviceClient.cs`) and these URLs too.

The plugin computes the shared race state once and sends a separate sequence of
these POSTs to every enabled display. The `features` list, brightness and
heartbeat are per display; timing is skipped when that display's tower page is
off and race logic when its logic page is off. Each display also has its own
connection backoff, so a timeout on one panel does not delay the others.

---

## Troubleshooting

| symptom | cause / fix |
|---------|-------------|
| **NO WIFI** on the display | The ESP32 has no station association. Check the router/SSID, move closer, or re-enter WiFi in the plugin. `WIFI: SETUP AP` means the fallback AP is active: connect to `CYD Flags fallback` and open `http://192.168.4.1`. |
| **NO SIMHUB** on the display | WiFi is fine but no heartbeat reaches the CYD. Start SimHub, select the display in the plugin, press **Test connection**, and check host/IP. The plugin backs off briefly after failures but keeps retrying. |
| Only **one of several** displays shows NO SIMHUB | Select that display card and check its host/IP. The other displays keep updating; each panel has its own connection and backoff. A stale display entry can be removed with **Remove selected**. |
| Plugin status says **404** | Entity name mismatch. Only rename entities if you also change `DeviceClient.cs`; the default names are `flag`, `game`, `race`, `timing`, `logic`, `lap_label`, `features`, `wifi_ssid`, `wifi_password`, `wifi_connect`, `sabt`, `heartbeat`, `brightness`. |
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
# pure logic tests (flags, delta tracker)
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
src/CydFlags/CydFlagsPlugin.cs   SimHub plugin lifecycle + per-display worker
src/CydFlags/CydFlagsSettings.cs global and per-display settings + 0.6 migration
src/CydFlags/FlagReader.cs       StatusDataBase flag reads (reflection fallback)
src/CydFlags/FlagModel.cs        priority selection / name parsing (tested)
src/CydFlags/TimingBuilder.cs    relative tower payload
src/CydFlags/RaceLogicBuilder.cs lap / sector / delta payload
src/CydFlags/RevBuilder.cs        rev-limiter payload
src/CydFlags/DeltaTracker.cs      previous-lap delta by track position
src/CydFlags/DeviceClient.cs     tiny POST client for ESPHome's web server
src/CydFlags/UI/                 settings panel (code-only WPF)
tests/                           pure C# tests (FlagModel + DeltaTracker)
dist/CydFlags.dll                prebuilt plugin
docs/cyd-flags.md                this file
```

## Credits / license

The display font is **DejaVu Sans Condensed Bold**, distributed under the
Bitstream Vera license; see `firmware/fonts/LICENSE-DejaVu.txt`. The code in
this repository is released under the MIT license; see `LICENSE`.
