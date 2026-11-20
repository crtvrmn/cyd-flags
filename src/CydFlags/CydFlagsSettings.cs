using System;
using System.Collections.Generic;

namespace CydFlags
{
    /// <summary>
    /// One CYD display. Everything that can differ between two panels lives
    /// here: address, enabled state, pages, brightness and WiFi provisioning.
    /// Persisted with JSON.net by SimHub as part of <see cref="CydFlagsSettings"/>.
    /// </summary>
    public class CydFlagsDeviceSettings
    {
        public string Name = "Display";

        public bool Enabled = true;

        /// <summary>Host name or IP of the CYD. The display shows its IP in the
        /// bottom-right corner. An optional http:// prefix and :port are stripped.</summary>
        public string Host = "cyd-flags.local";

        public int Port = 80;

        /// <summary>UDP port of the ESPHome fast data path. The firmware
        /// listens on 18512; HTTP is still used for configuration.</summary>
        public int UdpPort = 18512;

        /// <summary>Which pages the display should offer. The plugin sends this
        /// list to the ESP; the firmware hides disabled pages and the touch
        /// cycle skips them.</summary>
        public bool PageFlags = true;
        public bool PageClock = true;
        public bool PageTower = true;
        public bool PageLogic = true;
        public bool PageRev = true;

        /// <summary>Physical mounting of this display. "portrait" is the
        /// native 240x320 layout; "landscape" rotates it 90 degrees.</summary>
        public string Orientation = "portrait";

        /// <summary>Show each driver's best lap instead of the last lap in the
        /// race tower. The header above the tower says LAST or BEST.</summary>
        public bool ShowBestLap = false;

        /// <summary>Show the live delta bar to the previous lap on the race tower.</summary>
        public bool ShowDeltaBar = true;

        /// <summary>Send a brightness value when the plugin connects.</summary>
        public bool ApplyBrightness = false;

        /// <summary>Turn the backlight fully off while SimHub sees no running
        /// game. The configured Brightness is restored as soon as a game starts.</summary>
        public bool DimWhenNoGame = false;

        public int Brightness = 90;

        /// <summary>Last SSID used for this display's WiFi provisioning. The
        /// password is deliberately never persisted.</summary>
        public string WifiSsid = "";

        /// <summary>Comma-separated page list understood by the firmware.</summary>
        public string FeatureString()
        {
            string result = string.Empty;
            if (PageFlags) result = AppendFeature(result, "flags");
            if (PageClock) result = AppendFeature(result, "clock");
            if (PageTower) result = AppendFeature(result, "tower");
            if (PageLogic) result = AppendFeature(result, "logic");
            if (PageRev) result = AppendFeature(result, "rev");
            return result.Length == 0 ? "none" : result;
        }

        private static string AppendFeature(string current, string name)
        {
            return current.Length == 0 ? name : current + "," + name;
        }

        public void Clamp(int index)
        {
            if (string.IsNullOrWhiteSpace(Name)) Name = "Display " + (index + 1);
            Name = Name.Trim();

            if (string.IsNullOrWhiteSpace(Host)) Host = "cyd-flags.local";
            Host = Host.Trim();
            if (WifiSsid == null) WifiSsid = string.Empty;
            if (!string.Equals(Orientation, "landscape", StringComparison.OrdinalIgnoreCase))
                Orientation = "portrait";
            else
                Orientation = "landscape";

            Port = Math.Max(1, Math.Min(65535, Port));
            if (UdpPort <= 1 || UdpPort > 65535) UdpPort = 18512;
            Brightness = Math.Max(0, Math.Min(100, Brightness));
        }
    }

    /// <summary>
    /// Persisted with JSON.net by SimHub (ReadCommonSettings / SaveCommonSettings).
    /// Plain public fields, defaults sensible for a fresh install.
    ///
    /// 0.7.0 moved all display-specific values into <see cref="Devices"/>. The
    /// old single-device fields are kept so an existing 0.6.0 settings file
    /// still deserialises; <see cref="Clamp"/> turns them into the first device
    /// when no device list was saved yet.
    /// </summary>
    public class CydFlagsSettings
    {
        /// <summary>Global master switch. Individual displays also have their
        /// own <see cref="CydFlagsDeviceSettings.Enabled"/> flag.</summary>
        public bool Enabled = true;

        /// <summary>How often the plugin pings every display so it can show LIVE.
        /// The display turns to NO SIMHUB after 5 seconds without a heartbeat.</summary>
        public int HeartbeatMs = 1000;

        /// <summary>Minimum spacing between flag / game updates, to avoid
        /// flooding the ESP32 when a game flickers a flag for one frame.</summary>
        public int MinSendIntervalMs = 150;

        /// <summary>Log every HTTP request (useful while setting up).</summary>
        public bool LogRequests = false;

        /// <summary>When false (default), the tower/logic/delta pages are also
        /// produced in practice, qualifying and the menus. When true they are
        /// only produced during a race, leaving the big clock for everything
        /// else.</summary>
        public bool RacePagesOnly = false;

        /// <summary>The displays this plugin drives. At least one device exists
        /// after <see cref="Clamp"/>.</summary>
        public List<CydFlagsDeviceSettings> Devices = new List<CydFlagsDeviceSettings>();

        // ------------------------------------------------------------------
        // Legacy single-device fields (0.6.0 and older). Only read while
        // migrating; the settings UI no longer edits them.
        // ------------------------------------------------------------------
        public string Host = "cyd-flags.local";
        public int Port = 80;
        public bool ShowBestLap = false;
        public bool PageFlags = true;
        public bool PageClock = true;
        public bool PageTower = true;
        public bool PageLogic = true;
        public bool PageRev = true;

        /// <summary>Physical mounting of this display. "portrait" is the
        /// native 240x320 layout; "landscape" rotates it 90 degrees.</summary>
        public string Orientation = "portrait";
        public string WifiSsid = "";
        public bool ApplyBrightness = false;
        public bool DimWhenNoGame = false;
        public int Brightness = 90;

        public void Clamp()
        {
            HeartbeatMs = Math.Max(250, Math.Min(10000, HeartbeatMs));
            MinSendIntervalMs = Math.Max(50, Math.Min(5000, MinSendIntervalMs));

            if (Devices == null) Devices = new List<CydFlagsDeviceSettings>();

            // Migrate a 0.6.0 single-device settings file. A fresh install has
            // the same defaults, so this also creates the initial display.
            if (Devices.Count == 0)
                Devices.Add(DeviceFromLegacy());

            for (int i = 0; i < Devices.Count; i++)
            {
                if (Devices[i] == null) Devices[i] = new CydFlagsDeviceSettings();
                Devices[i].Clamp(i);
            }
        }

        /// <summary>Create and append a display with a unique name.</summary>
        public CydFlagsDeviceSettings AddDevice()
        {
            if (Devices == null) Devices = new List<CydFlagsDeviceSettings>();

            var device = new CydFlagsDeviceSettings();
            device.Name = NextDeviceName();
            Devices.Add(device);
            return device;
        }

        private CydFlagsDeviceSettings DeviceFromLegacy()
        {
            return new CydFlagsDeviceSettings
            {
                Name = "Display 1",
                Enabled = true,
                Host = string.IsNullOrWhiteSpace(Host) ? "cyd-flags.local" : Host.Trim(),
                Port = Port,
                UdpPort = 18512,
                PageFlags = PageFlags,
                PageClock = PageClock,
                PageTower = PageTower,
                PageLogic = PageLogic,
                PageRev = PageRev,
                Orientation = Orientation,
                ShowBestLap = ShowBestLap,
                ApplyBrightness = ApplyBrightness,
                DimWhenNoGame = DimWhenNoGame,
                Brightness = Brightness,
                WifiSsid = WifiSsid ?? string.Empty
            };
        }

        private string NextDeviceName()
        {
            for (int number = 1; number < 1000; number++)
            {
                string candidate = "Display " + number;
                bool used = false;
                if (Devices != null)
                {
                    foreach (CydFlagsDeviceSettings device in Devices)
                    {
                        if (device != null
                            && string.Equals(device.Name, candidate, StringComparison.OrdinalIgnoreCase))
                        {
                            used = true;
                            break;
                        }
                    }
                }

                if (!used) return candidate;
            }

            return "Display " + ((Devices != null ? Devices.Count : 0) + 1);
        }
    }
}
