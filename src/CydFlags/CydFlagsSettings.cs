using System;

namespace CydFlags
{
    /// <summary>
    /// Persisted with JSON.net by SimHub (ReadCommonSettings / SaveCommonSettings).
    /// Plain public fields, defaults sensible for a fresh install.
    /// </summary>
    public class CydFlagsSettings
    {
        public bool Enabled = true;

        /// <summary>Host name or IP of the CYD. The display shows its IP in the
        /// bottom-right corner. An optional http:// prefix and :port are stripped.</summary>
        public string Host = "cyd-flags.local";

        public int Port = 80;

        /// <summary>How often the plugin pings the display so it can show LIVE.
        /// The display turns to NO LINK after 5 seconds without a heartbeat.</summary>
        public int HeartbeatMs = 1000;

        /// <summary>Minimum spacing between flag / game updates, to avoid
        /// flooding the ESP32 when a game flickers a flag for one frame.</summary>
        public int MinSendIntervalMs = 150;

        /// <summary>Show each driver's best lap instead of the last lap in the
        /// race tower. The header above the tower says LAST or BEST.</summary>
        public bool ShowBestLap = false;

        /// <summary>Which pages the display should offer. The plugin sends this
        /// list to the ESP; the firmware hides disabled pages and the touch
        /// cycle skips them.</summary>
        public bool PageFlags = true;
        public bool PageClock = true;
        public bool PageTower = true;
        public bool PageLogic = true;

        /// <summary>Last SSID used for display WiFi provisioning. The password
        /// is deliberately never persisted.</summary>
        public string WifiSsid = "";

        /// <summary>Send a brightness value when the plugin connects.</summary>
        public bool ApplyBrightness = false;

        /// <summary>Turn the backlight fully off while SimHub sees no running
        /// game. The configured Brightness is restored as soon as a game starts.</summary>
        public bool DimWhenNoGame = false;

        public int Brightness = 90;

        /// <summary>Log every HTTP request (useful while setting up).</summary>
        public bool LogRequests = false;

        /// <summary>Comma-separated page list understood by the firmware.</summary>
        public string FeatureString()
        {
            string result = string.Empty;
            if (PageFlags) result = AppendFeature(result, "flags");
            if (PageClock) result = AppendFeature(result, "clock");
            if (PageTower) result = AppendFeature(result, "tower");
            if (PageLogic) result = AppendFeature(result, "logic");
            return result.Length == 0 ? "none" : result;
        }

        private static string AppendFeature(string current, string name)
        {
            return current.Length == 0 ? name : current + "," + name;
        }

        public void Clamp()
        {
            if (string.IsNullOrWhiteSpace(Host)) Host = "cyd-flags.local";
            Host = Host.Trim();
            if (WifiSsid == null) WifiSsid = string.Empty;

            Port = Math.Max(1, Math.Min(65535, Port));
            HeartbeatMs = Math.Max(250, Math.Min(10000, HeartbeatMs));
            MinSendIntervalMs = Math.Max(50, Math.Min(5000, MinSendIntervalMs));
            Brightness = Math.Max(0, Math.Min(100, Brightness));
        }
    }
}
