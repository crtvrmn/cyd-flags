# Changelog

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
