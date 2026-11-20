# Changelog

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
