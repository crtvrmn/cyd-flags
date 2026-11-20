using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CydFlags
{
    /// <summary>
    /// Fast realtime transport for the tower, rev and logic pages. Each call
    /// sends one small UDP datagram: one type byte, the UTF-8 payload and a
    /// newline. The ESPHome firmware (<c>udp:</c> component) decodes it without
    /// HTTP parsing, which keeps 20 Hz data cheap. Configuration (features,
    /// orientation, brightness, WiFi) still uses the HTTP API.
    /// </summary>
    internal sealed class UdpLink
    {
        private readonly Func<CydFlagsDeviceSettings> _settings;
        private readonly object _lock = new object();

        private UdpClient _socket;
        private IPEndPoint _endpoint;
        private string _endpointKey = string.Empty;
        private DateTime _lastResolveUtc = DateTime.MinValue;
        private string _lastError = string.Empty;
        private int _sent;

        public UdpLink(Func<CydFlagsDeviceSettings> settings)
        {
            _settings = settings;
        }

        public string LastError { get { lock (_lock) return _lastError; } }
        public int Sent { get { lock (_lock) return _sent; } }

        public bool SendFlag(string option) { return Send('f', option); }
        public bool SendGame(string game) { return Send('g', game); }
        public bool SendTiming(string payload) { return Send('t', payload); }
        public bool SendLogic(string payload) { return Send('l', payload); }
        public bool SendRev(string payload) { return Send('r', payload); }
        public bool SendDelta(string payload) { return Send('d', payload); }

        public bool SendHeartbeat(int counter)
        {
            return Send('h', counter.ToString());
        }

        private bool Send(char type, string payload)
        {
            if (!EnsureEndpoint()) return false;

            byte[] body = Encoding.UTF8.GetBytes(payload ?? string.Empty);
            byte[] packet = new byte[body.Length + 2];
            packet[0] = (byte)type;
            Buffer.BlockCopy(body, 0, packet, 1, body.Length);
            packet[packet.Length - 1] = (byte)'\n';

            try
            {
                lock (_lock)
                {
                    _socket.Send(packet, packet.Length, _endpoint);
                    _sent++;
                }
                return true;
            }
            catch (Exception ex)
            {
                lock (_lock) _lastError = ex.Message;
                return false;
            }
        }

        private bool EnsureEndpoint()
        {
            CydFlagsDeviceSettings settings = _settings();
            if (settings == null) return false;

            string host = CleanHost(settings.Host);
            int port = settings.UdpPort;
            if (string.IsNullOrEmpty(host)) return false;
            if (port < 1 || port > 65535) port = 18512;

            string key = host + ":" + port;
            lock (_lock)
            {
                if (_endpoint != null && _endpointKey == key) return true;

                // Re-resolving a name on every packet would be wasteful and can
                // stall the worker; a failed lookup is retried at most once a
                // minute.
                if (_endpoint == null && _lastResolveUtc != DateTime.MinValue
                    && (DateTime.UtcNow - _lastResolveUtc).TotalSeconds < 60)
                {
                    return false;
                }

                try
                {
                    IPAddress address;
                    if (!IPAddress.TryParse(host, out address))
                    {
                        IPAddress[] addresses = Dns.GetHostAddresses(host);
                        address = null;
                        foreach (IPAddress candidate in addresses)
                        {
                            if (candidate.AddressFamily == AddressFamily.InterNetwork)
                            {
                                address = candidate;
                                break;
                            }
                        }
                        if (address == null)
                        {
                            _lastError = "no IPv4 address for " + host;
                            _lastResolveUtc = DateTime.UtcNow;
                            return false;
                        }
                    }

                    if (_socket != null)
                    {
                        try { _socket.Close(); } catch (Exception) { }
                    }

                    _socket = new UdpClient(address.AddressFamily);
                    _endpoint = new IPEndPoint(address, port);
                    _endpointKey = key;
                    _lastResolveUtc = DateTime.UtcNow;
                    _lastError = string.Empty;
                    return true;
                }
                catch (Exception ex)
                {
                    _lastError = "UDP " + host + ": " + ex.Message;
                    _lastResolveUtc = DateTime.UtcNow;
                    return false;
                }
            }
        }

        private static string CleanHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return string.Empty;
            host = host.Trim();
            if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                host = host.Substring(7);
            else if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                host = host.Substring(8);
            int slash = host.IndexOf('/');
            if (slash >= 0) host = host.Substring(0, slash);
            // Only strip a simple :port suffix, not an IPv6 literal.
            int firstColon = host.IndexOf(':');
            int lastColon = host.LastIndexOf(':');
            if (firstColon > 0 && firstColon == lastColon)
                host = host.Substring(0, firstColon);
            return host.Trim();
        }
    }
}
