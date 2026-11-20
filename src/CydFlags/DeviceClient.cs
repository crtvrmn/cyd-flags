using System;
using System.IO;
using System.Net;
using System.Globalization;

namespace CydFlags
{
    /// <summary>
    /// Tiny HTTP client for the ESPHome web server REST API. ESPHome only
    /// accepts POST for entity setters, so every call is a POST with no body
    /// and all parameters in the query string.
    /// </summary>
    internal sealed class DeviceClient
    {
        private readonly Func<CydFlagsDeviceSettings> _settings;
        private readonly object _lock = new object();

        private bool _reachable;
        private string _lastError = string.Empty;
        private string _lastUrl = string.Empty;
        private DateTime _lastOkUtc;
        private int _requests;

        static DeviceClient()
        {
            // Avoid the 100-continue round trip; the ESP32 does not need it.
            ServicePointManager.Expect100Continue = false;
        }

        public DeviceClient(Func<CydFlagsDeviceSettings> settings)
        {
            _settings = settings;
        }

        public bool Reachable { get { lock (_lock) return _reachable; } }
        public string LastError { get { lock (_lock) return _lastError; } }
        public string LastUrl { get { lock (_lock) return _lastUrl; } }
        public DateTime LastOkUtc { get { lock (_lock) return _lastOkUtc; } }
        public int Requests { get { lock (_lock) return _requests; } }

        public void ResetStatus()
        {
            lock (_lock)
            {
                _reachable = false;
                _lastError = string.Empty;
                _lastUrl = string.Empty;
                _lastOkUtc = default(DateTime);
            }
        }

        public bool SendFlag(FlagKind kind)
        {
            return Post("select/flag/set?option=" + Uri.EscapeDataString(FlagModel.ToOption(kind)));
        }

        public bool SendGame(string game)
        {
            return Post("text/game/set?value=" + Uri.EscapeDataString(game ?? string.Empty));
        }

        public bool SendRaceMode(bool race)
        {
            return Post("text/race/set?value=" + (race ? "RACE" : "CLOCK"));
        }

        public bool SendTiming(string payload)
        {
            return Post("text/timing/set?value=" + Uri.EscapeDataString(payload ?? string.Empty));
        }

        public bool SendLapLabel(string label)
        {
            return Post("text/lap_label/set?value=" + Uri.EscapeDataString(label ?? string.Empty));
        }

        public bool SendLogic(string payload)
        {
            return Post("text/logic/set?value=" + Uri.EscapeDataString(payload ?? string.Empty));
        }

        public bool SendDelta(string payload)
        {
            return Post("text/delta/set?value=" + Uri.EscapeDataString(payload ?? string.Empty));
        }

        public bool SendRev(string payload)
        {
            return Post("text/rev/set?value=" + Uri.EscapeDataString(payload ?? string.Empty));
        }

        public bool SendOrientation(string orientation)
        {
            return Post("text/orientation/set?value=" + Uri.EscapeDataString(orientation ?? "portrait"));
        }

        public bool SendFeatures(string features)
        {
            return Post("text/features/set?value=" + Uri.EscapeDataString(features ?? string.Empty));
        }

        public bool SendWifiSsid(string ssid)
        {
            return Post("text/wifi_ssid/set?value=" + Uri.EscapeDataString(ssid ?? string.Empty));
        }

        public bool SendWifiPassword(string password)
        {
            return Post("text/wifi_password/set?value=" + Uri.EscapeDataString(password ?? string.Empty));
        }

        public bool SendWifiConnect()
        {
            return Post("button/wifi_connect/press");
        }

        public bool SendHeartbeat(int counter)
        {
            return Post("number/heartbeat/set?value=" + counter.ToString());
        }

        public bool SendBrightness(int percent)
        {
            return Post("number/brightness/set?value=" + percent.ToString());
        }


        /// <summary>Reads the display's UDP receive counter (sensor udp_rx).
        /// Returns false on old firmware or HTTP errors; callers treat that as
        /// "UDP status unknown" rather than a device failure.</summary>
        public bool TryGetUdpRx(out int count, out string error)
        {
            count = 0;
            error = string.Empty;
            string url;
            try
            {
                url = BuildUrl("sensor/udp_rx");
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = 700;
                request.ReadWriteTimeout = 700;
                request.KeepAlive = false;
                request.Proxy = null;
                request.UserAgent = "CydFlags/0.1";

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    string body = reader.ReadToEnd();
                    int marker = body.IndexOf("\"state\":\"", StringComparison.Ordinal);
                    if (marker < 0)
                        marker = body.IndexOf("\"value\":\"", StringComparison.Ordinal);
                    if (marker < 0)
                    {
                        error = "udp_rx: unexpected JSON";
                        return false;
                    }
                    marker = body.IndexOf(':', marker) + 1;
                    int start = body.IndexOf('"', marker) + 1;
                    int end = body.IndexOf('"', start);
                    if (start <= 0 || end <= start)
                    {
                        error = "udp_rx: unexpected JSON";
                        return false;
                    }
                    string state = body.Substring(start, end - start);
                    if (!int.TryParse(state, NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
                    {
                        error = "udp_rx: not a number";
                        return false;
                    }
                    return true;
                }
            }
            catch (WebException ex)
            {
                var http = ex.Response as HttpWebResponse;
                if (http != null)
                {
                    error = "udp_rx HTTP " + (int)http.StatusCode;
                    http.Dispose();
                }
                else
                {
                    error = "udp_rx " + ex.Message;
                }
                return false;
            }
            catch (Exception ex)
            {
                error = "udp_rx " + ex.Message;
                return false;
            }
        }

        private bool Post(string relative)
        {
            string url;
            try
            {
                url = BuildUrl(relative);
            }
            catch (Exception ex)
            {
                SetFailure(ex.Message, string.Empty);
                return false;
            }

            lock (_lock)
            {
                _requests++;
                _lastUrl = url;
            }

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentLength = 0;
                request.ContentType = "application/x-www-form-urlencoded";
                request.Timeout = 700;
                request.ReadWriteTimeout = 700;
                request.KeepAlive = false;
                request.Proxy = null;
                request.UserAgent = "CydFlags/0.1";

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    int code = (int)response.StatusCode;
                    using (Stream stream = response.GetResponseStream())
                    {
                        if (stream != null)
                        {
                            // Drain the tiny response so the socket can close cleanly.
                            var buffer = new byte[64];
                            stream.Read(buffer, 0, buffer.Length);
                        }
                    }

                    if (code >= 200 && code < 300)
                    {
                        lock (_lock)
                        {
                            _reachable = true;
                            _lastError = string.Empty;
                            _lastOkUtc = DateTime.UtcNow;
                        }
                        return true;
                    }

                    SetFailure("HTTP " + code + " " + response.StatusDescription, url);
                    return false;
                }
            }
            catch (WebException ex)
            {
                string message = ex.Message;
                var http = ex.Response as HttpWebResponse;
                if (http != null)
                {
                    int code = (int)http.StatusCode;
                    message = "HTTP " + code + " " + http.StatusDescription;
                    if (code == 404)
                        message += " (entity name mismatch - check the firmware YAML)";
                    http.Dispose();
                }
                else if (ex.Status == WebExceptionStatus.Timeout)
                {
                    message = "timeout (is the display powered and on WiFi?)";
                }
                else if (ex.Status == WebExceptionStatus.NameResolutionFailure)
                {
                    message = "host not found (use the IP shown on the display)";
                }

                SetFailure(message, url);
                return false;
            }
            catch (Exception ex)
            {
                SetFailure(ex.Message, url);
                return false;
            }
        }

        private void SetFailure(string message, string url)
        {
            lock (_lock)
            {
                _reachable = false;
                _lastError = message;
                if (!string.IsNullOrEmpty(url)) _lastUrl = url;
            }
        }

        private string BuildUrl(string relative)
        {
            CydFlagsDeviceSettings settings = _settings != null ? _settings() : null;
            if (settings == null) throw new InvalidOperationException("device settings unavailable");

            string host = (settings.Host ?? string.Empty).Trim();
            if (host.Length == 0) throw new InvalidOperationException("device host is empty");

            if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                host = host.Substring(7);
            else if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                host = host.Substring(8);

            int slash = host.IndexOf('/');
            if (slash >= 0) host = host.Substring(0, slash);

            int port = settings.Port;
            int colon = host.LastIndexOf(':');
            if (colon > 0)
            {
                int parsed;
                if (int.TryParse(host.Substring(colon + 1), out parsed))
                {
                    port = parsed;
                    host = host.Substring(0, colon);
                }
            }

            if (host.Length == 0) throw new InvalidOperationException("device host is empty");

            return "http://" + host + ":" + port + "/" + relative;
        }
    }
}
