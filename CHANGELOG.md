# Changelog

## 0.8.1

* **SABT page fix.** The released SABT plugin exposes `SABT.MinimumTension`,
  not only `SABT.RestingTension`; both are accepted now. The page is reachable
  whenever SABT is enabled and the display has a link, and shows **WAITING**
  with `--` until the first SABT frame arrives instead of disappearing.
* **Trend disc stays visible.** Blue is the neutral state; a green/red disc
  remains until the gap direction actually changes instead of vanishing on
  every unchanged sample.
* **Two-car/ghost fix.** The tower now picks the neighbours from their overall
  position and never shows the same opponent in both rows.
* Flags keep absolute priority over every page.
* Plugin and firmware 0.8.1.

## 0.8.0

* **SABT page.** New page shows the Simple Active Belt Tensioner state
  (**ACTIVE** / **OFF**) and the idle, resting and maximum tension values. The
  plugin reads `SABT.IsEnabled`, `SABT.IdleTension`, `SABT.MinimumTension`
  (resting alias) and `SABT.MaximumTension`; SABT is optional, the page only
  appears when the page is enabled (it shows WAITING until data arrives).
* **Cleaner plugin UI.** The heartbeat, minimum-gap, HTTP/UDP port and request
  log fields are gone — the plugin manages those values. Displays are shown as
  clickable cards with name, host and enabled state, so several panels cannot be
  confused. Add/remove and **Open display web UI** remain.
* **SABT page checkbox** per display; feature token `sabt`, entity `sabt`.
* Plugin and firmware 0.8.0.

## 0.7.12

* **Race tower shows a trend disc instead of a gap.** A green **+** means you
  are gaining, a red **−** means you are losing; no numeric gap is drawn. The
  disc uses the same direction rules as the old green/red gap text.
* **Race logic no longer draws the current-lap time.** With the freed space the
  delta and sector cards are larger and easier to read.
* **Rev page is gear + km/h only**, with no RPM bar or RPM text. The page is
  redrawn at 33 Hz (30 ms) and the plugin builds rev at 50 Hz, so the speed
  reacts noticeably faster.
* **Heartbeat default is 50 ms**, with the HTTP keepalive throttled to 1 s while
  UDP is unconfirmed and 5 s afterwards. A 1 ms heartbeat is a link ping, not a
  refresh rate; it would just flood the ESP32 and is clamped to 20 ms minimum.
* Plugin 0.7.12, firmware 0.7.12.

## 0.7.11

* Firmware version is exposed as the text entity `firmware_version`, visible in
  the ESPHome web UI and via `GET /text/firmware_version`; no need to catch the
  8-second boot splash.
* Plugin reports a 404 on `text/udp_rx` as "display firmware too old" instead of
  a bare HTTP error.
* Plugin 0.7.11, firmware 0.7.11.

## 0.7.10

* **UDP confirmation via `text/udp_rx`.** This ESPHome build returns an empty
  reply for `/sensor/...`, so the plugin could never read the UDP counter and
  stayed at `UDP: unknown` with an error. The counter is now a text entity;
  the plugin reads `/text/udp_rx`, confirms real UDP delivery, and only then
  stops duplicating realtime payloads over HTTP.
* Plugin 0.7.10, firmware 0.7.9.

## 0.7.9

* **Flags and test flags always go over HTTP too.** The previous UDP-first
  send returned success even when the datagram was filtered, so flags (and the
  plugin's flag test buttons) could disappear completely. Flags/game are now
  pushed over both transports; they are low-rate, so this is cheap.
* **On-screen UDP warning.** If the display is linked over HTTP but has not
  received a UDP packet for 10 s, it shows a small `UDP BLOCKED` badge in the
  top-right corner, so a blocked fast path is visible on the CYD itself.
* Plugin 0.7.9, firmware 0.7.8.

## 0.7.8

* **HTTP fallback until UDP is confirmed.** The plugin sends tower, logic and
  rev payloads over HTTP at up to 5 Hz until the display's `udp_rx` counter has
  moved at least once. A blocked UDP port (firewall, wrong port, old firmware)
  therefore no longer leaves the display with only the clock/`NO PAGE`, while
  confirmed UDP still updates at 20 Hz.
* `UdpPort` values `0`/`1` are migrated to `18512`; a newly added display uses
  `18512`.
* Plugin 0.7.8; firmware 0.7.7 is sufficient.

## 0.7.7

* **UDP diagnosis in the plugin.** The display now exposes a `udp_rx` counter
  over HTTP. The plugin reads it every five seconds and shows one of
  `UDP: OK (display received N)`, `UDP: blocked - tower will be slow` or
  `UDP: unknown` in its settings status. This makes a firewall/port problem
  visible without guessing. The HTTP heartbeat still keeps the link alive.
* Plugin and firmware 0.7.7; the boot splash shows `v0.7.7`.

## 0.7.6

* **Boot splash with firmware version.** The display shows `CYD FLAGS` and the
  firmware version for eight seconds after every boot, then switches to the
  normal flag/clock/race screen. This makes it easy to check which image is
  actually running.
* **Fixed `NO SIMHUB` after 0.7.5.** The plugin now sends the HTTP heartbeat in
  addition to the UDP heartbeat. If UDP is blocked by a firewall, the host name
  does not resolve, or the display still runs older firmware without the UDP
  receiver, the link stays alive over HTTP. Both plugin and firmware 0.7.6 are
  recommended; the plugin alone also fixes the link status with older firmware.

## 0.7.5

* **UDP fast path.** Realtime flags, game name, tower, rev, race-logic, delta
  and the heartbeat no longer travel over HTTP. The plugin sends one small
  datagram per value to UDP port 18512; the firmware decodes it without HTTP
  parsing. HTTP is still used for configuration (features, orientation,
  brightness, WiFi) and manual tests.
* **Much faster tower.** The plugin builds the tower at 20 Hz (was 2.5 Hz) and
  the display redraws it at 80 ms (12.5 Hz, was 300 ms). Rev stays at 20 Hz,
  race logic builds at 10 Hz. The clock/flag screens keep their 300 ms cadence.
* **No more 0.7.4 partial-update bottleneck for the tower**: the full tower
  frame is intentionally redrawn at 12.5 Hz; the rev page still uses the
  partial redraw from 0.7.4.
* Plugin 0.7.5 and firmware 0.7.5 belong together. If the UDP port is blocked
  the plugin falls back to HTTP, but the tower will be slower again.
* New `UdpPort` per-display setting (default 18512).

## 0.7.4

* **Rev page partial redraw at 20 Hz.** The rev-limiter page no longer repaints
  the whole 240x320 panel on every frame. The display driver now transfers only
  the dirty rectangle: the RPM bar is redrawn alone, the gear/speed only when
  their value changes, and a new page or blink phase still triggers one clean
  full frame. The page polls at 50 ms and the plugin builds/sends `rev` at 20 Hz
  (was 10 Hz). The normal clock/flag/tower pages keep their 300 ms cadence.
* **Why not 50-60 FPS?** A full 240x320 RGB565 frame is ~153 KB; 50 FPS would
  need >60 Mbit/s, more than the CYD's 40 MHz SPI link can carry. Partial
  updates avoid that ceiling for the small rev widgets, but the ESPHome HTTP
  API and the 40 MHz panel bus still cap the practical update rate well below
  50 FPS. LVGL would need the same bandwidth for a full-screen image.
* Firmware + plugin change; protocol unchanged. Flash firmware 0.7.4 and update
  the plugin together.

## 0.7.3

* **Race pages outside a race.** A new global plugin option controls whether the
  relative tower, race logic and previous-lap delta are also produced in
  practice, qualifying and the menus. It is **unchecked by default**, so those
  pages now work everywhere; check *Only show tower/logic/delta during a race*
  to restore the old race-only behaviour and keep the big clock in practice.
* Plugin-side change only; firmware 0.7.2 and newer already understands the
  `RACE` mode from the plugin.

## 0.7.2

* **Rev limiter refreshes at 10 Hz.** The display now switches its own redraw
  interval from 300 ms to 100 ms while the rev-limiter page is visible, matching
  the plugin's 100 ms rev payload. The clock/flag/tower pages keep the calmer
  300 ms cadence.
* **Touch X axis corrected.** The CYD's XPT2046 reports the horizontal axis
  mirrored on this panel. The firmware now sets `transform.mirror_x: true`, so
  the alarm setup quadrants (+1 h / +5 min / off / save) and the bottom-left /
  bottom-right page navigation match what is printed on screen.
* Firmware-side changes; the plugin protocol is unchanged. Plugin 0.7.2 is a
  version bump only and stays compatible with firmware 0.7.x.

## 0.7.1

* **Clear connection status.** The CYD no longer shows the generic `NO LINK`.
  A missing WiFi association shows **NO WIFI** (or **WIFI: SETUP AP** while the
  fallback access point is active); WiFi without a SimHub heartbeat shows
  **NO SIMHUB**. When both links are up the status line is omitted, so the
  normal clock/date screen is not cluttered with redundant "OK" text.
* This is a firmware-side change; plugin 0.7.1 has no protocol changes and is
  compatible with firmware 0.7.x.

## 0.7.0

* **Multiple CYD displays at once.** Each display has its own name, host/port,
  enabled flag, page selection, `LAST`/`BEST` choice, brightness/dim behaviour
  and WiFi provisioning.
* **Corner navigation on the CYD.** A 24 px dead cross splits the panel; bottom
  right = next page, bottom left = previous page. The order is
  tower → race logic → rev limiter → clock. Flags keep priority; the top half
  is reserved for the clock/alarm controls.
* **Rev-limiter page.** Large gear, speed in km/h, and an RPM bar that blinks
  red at the redline. Available in races, practice and qualifying.
* **Per-display orientation.** Portrait 240×320 or landscape 320×240 is selected
  in the plugin and applied at runtime with `screen.set_rotation(...)`; one
  firmware image supports both mountings.
* **Previous-lap delta bar.** `DeltaTracker` samples `CurrentLapTime` against
  `TrackPositionPercent` and interpolates the previous lap at the same position.
  Green right = faster, orange left = slower; hides on the first lap and after
  a reset.
* **Daily alarm on the CYD.** Tap the small alarm icon top-left on the clock,
  then set the time with the quadrants (+1 h / +5 min / off / save). The alarm
  fires once per day, shows a flashing full-screen ALARM, and can play an
  optional best-effort tone on GPIO26.
* **Plugin settings panel** gained per-display page checkboxes (including Rev
  limiter), the orientation combo, the delta-bar checkbox, and the alarm/touch
  hints.
* Firmware and HTTP API additions: `text/orientation`, `text/rev`,
  `text/delta`, plus the alarm globals. Plugin 0.7.0 and firmware 0.7.0 belong
  together.
* Version bumped to 0.7.0.

## 0.6.0

* Plugin **Pages on the display** checkboxes (flags, clock, tower, race logic);
  the firmware persists the list and the touch cycle skips disabled pages.
* **Open display web UI** button in the plugin.
* **Save WiFi on display**: the plugin writes `wifi_ssid`/`wifi_password` and
  presses the firmware's `wifi_connect` button (`wifi.configure`, `save: true`).
  Credentials live in NVS and survive OTA updates and reboots.
* **Test the flags**: show any flag for 10 seconds from the plugin without a
  running game, then resume normal data.
* Plugin and firmware now expose `features`, `wifi_ssid`, `wifi_password` and
  `wifi_connect` entities.

## 0.5.0

* Added the **RACELOGIC** page: current lap, sector times with green/red
  backgrounds and delta to the best lap.
* Touch now cycles **race tower → RACELOGIC → clock → race tower**.
* Fixed the dim-when-no-game option: brightness is restored as soon as a game
  is running again.
* Plugin payload for the race tower was reduced to six fields per row (sector
  times moved to the new page).

## 0.4.0

* Removed sector times from the race tower.
* Removed the meaningless `0.00` gap from the player row.
* Removed the per-row `LAST` label; the header now shows `LAST`/`BEST`
  (configurable in the plugin) and `PIT` while in the pit lane.
* Larger driver and lap-time text, smaller position number.

## 0.3.0

* Coloured race tower: gap trend and purple/red/green time backgrounds.
* Gap colours: green while closing, red while losing time; reversed for the
  car behind.
* Colour-coded times: purple = fastest lap/sector, red = faster than you,
  green = slower than you.
* Accent/umlaut folding for driver names (ASCII-safe glyphs).

## 0.2.0

* Added the clock outside races and the relative race tower.
* Added `text` entities for race mode and timing.
* Added `TimingBuilder` and POST support for race/timing.

## 0.1.0

* Initial CYD Flags firmware: flags, clock and `NO LINK` state.
* Initial SimHub plugin with flag priority logic, heartbeat and brightness.
* Prebuilt firmware for ILI9341, ST7789V and ILI9342 panels.
