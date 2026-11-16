using System;
using System.Text;
using System.Threading;
using GameReaderCommon;
using SimHub.Plugins;

namespace CydFlags
{
    /// <summary>
    /// CYD Flags: pushes the current sim racing flag to a Cheap Yellow Display
    /// running ESPHome, using the ESPHome web server REST API. No MQTT and no
    /// Home Assistant are required - just the display on the same LAN.
    /// </summary>
    [PluginDescription("Shows flags, a clock and relative race timing on an ESP32 'Cheap Yellow Display' (ESPHome firmware over HTTP)")]
    [PluginAuthor("crt")]
    [PluginName("CYD Flags")]
    public class CydFlagsPlugin : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        public const string Version = "0.5.0";

        public CydFlagsSettings Settings;

        private readonly object _stateLock = new object();

        private FlagKind _flag = FlagKind.None;
        private string _game = string.Empty;
        private bool _raceMode;
        private bool _gameRunning;
        private string _timing = string.Empty;
        private string _logic = string.Empty;
        private string _lapLabel = "LAST";
        private bool _reachable;
        private string _lastError = string.Empty;
        private string _lastUrl = string.Empty;
        private DateTime _lastOkUtc;

        private DeviceClient _client;

        private Thread _worker;
        private AutoResetEvent _wake;
        private volatile bool _stopping;

        // Worker-thread only state.
        private string _lastSentFlag;
        private string _lastSentGame;
        private string _lastSentRaceMode;
        private string _lastSentTiming;
        private string _lastSentLogic;
        private string _lastSentLapLabel;
        private string _lastHostKey;
        private DateTime _lastFlagSentUtc;
        private DateTime _lastGameSentUtc;
        private DateTime _lastHeartbeatUtc;
        private DateTime _lastRaceModeSentUtc;
        private DateTime _lastTimingSentUtc;
        private DateTime _lastLogicSentUtc;
        private DateTime _lastLapLabelSentUtc;
        private DateTime _backoffUntilUtc;
        private int _heartbeatCounter;
        private bool _brightnessDirty;
        private int _lastSentBrightness = -1;
        private bool _lastGameRunning;

        private static readonly DateTime NoTiming = DateTime.MinValue;
        private DateTime _lastTimingBuildUtc = NoTiming;
        private DateTime _lastLogicBuildUtc = NoTiming;

        // Log throttling.
        private string _lastLoggedError;
        private DateTime _lastErrorLogUtc;

        private double _lastDataErrorLogSeconds;
        private string _lastDataError;

        public PluginManager PluginManager { get; set; }

        public System.Windows.Media.ImageSource PictureIcon { get { return null; } }

        public string LeftMenuTitle { get { return "CYD Flags"; } }

        // ------------------------------------------------------------------
        // Read-only state for the settings UI and SimHub properties.
        // ------------------------------------------------------------------
        public bool Linked { get { lock (_stateLock) return _reachable; } }
        public string LastError { get { lock (_stateLock) return _lastError; } }
        public string DeviceUrl { get { lock (_stateLock) return _lastUrl; } }
        public DateTime LastReplyUtc { get { lock (_stateLock) return _lastOkUtc; } }
        public FlagKind CurrentFlag { get { lock (_stateLock) return _flag; } }
        public string CurrentGame { get { lock (_stateLock) return _game; } }
        public bool RaceMode { get { lock (_stateLock) return _raceMode; } }
        public string CurrentTiming { get { lock (_stateLock) return _timing; } }
        public string CurrentLogic { get { lock (_stateLock) return _logic; } }
        public int RequestsSent { get { return _client != null ? _client.Requests : 0; } }

        public void Init(PluginManager pluginManager)
        {
            try
            {
                SimHub.Logging.Current.Info("CYD Flags " + Version + " initialising");

                Settings = this.ReadCommonSettings<CydFlagsSettings>("CydFlagsSettings", () => new CydFlagsSettings());
                if (Settings == null) Settings = new CydFlagsSettings();
                Settings.Clamp();

                _client = new DeviceClient(() => Settings);
                _wake = new AutoResetEvent(false);
                _stopping = false;
                _worker = new Thread(WorkerLoop);
                _worker.IsBackground = true;
                _worker.Name = "CYD Flags HTTP";
                _worker.Start();

                // Cheap pull-based properties: the value is only computed when
                // ShakeIt or the property browser asks for it.
                this.AttachDelegate("CydFlagsLinked", () => Linked ? 1.0 : 0.0);
                this.AttachDelegate("CydFlagsFlag", () => FlagModel.Describe(CurrentFlag));
                this.AttachDelegate("CydFlagsGame", () => CurrentGame);
                this.AttachDelegate("CydFlagsDevice", () => DeviceUrl);
                this.AttachDelegate("CydFlagsError", () => LastError);
                this.AddAction("CydFlagsTest", (a, b) => TestConnection());

                SimHub.Logging.Current.Info("CYD Flags: device target " + DescribeTarget());
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error("CYD Flags: Init failed: " + ex);
            }
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            if (Settings == null) return;

            try
            {
                if (!Settings.Enabled)
                {
                    SetGameRunning(false);
                    SetState(FlagKind.None, string.Empty, false, string.Empty, string.Empty, "LAST");
                    return;
                }

                if (!data.GameRunning || data.NewData == null)
                {
                    SetGameRunning(false);
                    SetState(FlagKind.None, string.Empty, false, string.Empty, string.Empty, "LAST");
                    _lastTimingBuildUtc = NoTiming;
                    _lastLogicBuildUtc = NoTiming;
                    TimingBuilder.ResetTrend();
                    return;
                }

                SetGameRunning(true);

                FlagKind kind = FlagReader.ReadKind(data.NewData);
                string game = data.GameName ?? string.Empty;

                bool race = TimingBuilder.IsRaceSession(data.NewData);
                string timing = string.Empty;
                string logic = string.Empty;
                if (race)
                {
                    DateTime now = DateTime.UtcNow;
                    if ((now - _lastTimingBuildUtc).TotalMilliseconds >= 400)
                    {
                        timing = TimingBuilder.Build(data.NewData, Settings.ShowBestLap);
                        _lastTimingBuildUtc = now;
                    }
                    else
                    {
                        lock (_stateLock) timing = _timing;
                    }

                    if ((now - _lastLogicBuildUtc).TotalMilliseconds >= 400)
                    {
                        logic = RaceLogicBuilder.Build(data.NewData);
                        _lastLogicBuildUtc = now;
                    }
                    else
                    {
                        lock (_stateLock) logic = _logic;
                    }
                }
                else
                {
                    _lastTimingBuildUtc = NoTiming;
                    _lastLogicBuildUtc = NoTiming;
                    TimingBuilder.ResetTrend();
                }

                bool pit = Convert.ToBoolean(data.NewData.IsInPitLane);
                string lapLabel = (Settings.ShowBestLap ? "BEST" : "LAST") + (pit ? " PIT" : "");
                SetState(kind, game, race, timing, logic, lapLabel);
            }
            catch (Exception ex)
            {
                // DataUpdate is on SimHub's critical path: never throw, and do
                // not spam the log at 60 Hz either.
                double now = (DateTime.UtcNow - DateTime.MinValue).TotalSeconds;
                if (ex.Message != _lastDataError || now - _lastDataErrorLogSeconds > 10)
                {
                    _lastDataError = ex.Message;
                    _lastDataErrorLogSeconds = now;
                    SimHub.Logging.Current.Error("CYD Flags: DataUpdate failed: " + ex);
                }
            }
        }

        public void End(PluginManager pluginManager)
        {
            try
            {
                this.SaveCommonSettings("CydFlagsSettings", Settings);
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error("CYD Flags: saving settings failed: " + ex);
            }

            _stopping = true;
            try
            {
                if (_wake != null) _wake.Set();
                if (_worker != null) _worker.Join(1500);
            }
            catch (Exception)
            {
                // Shutting down.
            }
            _worker = null;
            SimHub.Logging.Current.Info("CYD Flags stopped");
        }

        public System.Windows.Controls.Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            return new UI.CydFlagsSettingsView(this);
        }

        /// <summary>Called by the settings UI after a value changed.</summary>
        public void ApplySettings()
        {
            if (Settings == null) return;
            Settings.Clamp();
            if (Settings.ApplyBrightness || Settings.DimWhenNoGame) _brightnessDirty = true;
            if (_wake != null) _wake.Set();

            try
            {
                this.SaveCommonSettings("CydFlagsSettings", Settings);
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error("CYD Flags: saving settings failed: " + ex);
            }
        }

        /// <summary>Force a fresh flag/game/heartbeat push, used by the Test button.</summary>
        public void TestConnection()
        {
            _lastSentFlag = null;
            _lastSentGame = null;
            _lastSentRaceMode = null;
            _lastSentTiming = null;
            _lastSentLogic = null;
            _lastSentBrightness = -1;
            _lastHeartbeatUtc = DateTime.MinValue;
            _backoffUntilUtc = DateTime.MinValue;
            if (_client != null) _client.ResetStatus();
            if (_wake != null) _wake.Set();
        }

        /// <summary>Push the brightness slider value to the display now.</summary>
        public void ApplyBrightnessNow()
        {
            _brightnessDirty = true;
            _backoffUntilUtc = DateTime.MinValue;
            if (_wake != null) _wake.Set();
        }

        public string DescribeTarget()
        {
            if (Settings == null) return "-";
            return "http://" + Settings.Host + ":" + Settings.Port;
        }

        // ------------------------------------------------------------------
        // Worker
        // ------------------------------------------------------------------
        private void WorkerLoop()
        {
            while (!_stopping)
            {
                try
                {
                    Pump();
                }
                catch (Exception ex)
                {
                    // A worker exception must never kill the thread.
                    LogErrorThrottled("worker error: " + ex.Message);
                }

                if (_wake != null) _wake.WaitOne(25);
            }
        }

        private void Pump()
        {
            CydFlagsSettings settings = Settings;
            if (settings == null || !settings.Enabled) return;

            DateTime now = DateTime.UtcNow;

            string hostKey = (settings.Host ?? string.Empty) + "|" + settings.Port;
            if (hostKey != _lastHostKey)
            {
                _lastHostKey = hostKey;
                _lastSentFlag = null;
                _lastSentGame = null;
                _lastSentRaceMode = null;
                _lastSentTiming = null;
                _lastSentLogic = null;
                _lastSentLapLabel = null;
                _lastSentBrightness = -1;
                _lastHeartbeatUtc = DateTime.MinValue;
                _lastRaceModeSentUtc = DateTime.MinValue;
                _lastTimingSentUtc = DateTime.MinValue;
                _lastLogicSentUtc = DateTime.MinValue;
                _lastLapLabelSentUtc = DateTime.MinValue;
                _backoffUntilUtc = DateTime.MinValue;
                if (_client != null) _client.ResetStatus();
                _brightnessDirty = settings.ApplyBrightness || settings.DimWhenNoGame;
            }

            if (now < _backoffUntilUtc) return;

            FlagKind flag;
            string game;
            bool race;
            bool gameRunning;
            string timing;
            string logic;
            string lapLabel;
            lock (_stateLock)
            {
                flag = _flag;
                game = _game;
                race = _raceMode;
                gameRunning = _gameRunning;
                timing = _timing;
                logic = _logic;
                lapLabel = _lapLabel;
            }

            // Make sure a game start always re-sends the slider brightness,
            // even if the previous target happened to be the same value. This
            // is what restores the panel after the idle 0% dim.
            if (gameRunning != _lastGameRunning)
            {
                _lastGameRunning = gameRunning;
                _brightnessDirty = true;
            }

            string option = FlagModel.ToOption(flag);
            string safeGame = SanitizeGame(game);

            if (option != _lastSentFlag
                && (now - _lastFlagSentUtc).TotalMilliseconds >= settings.MinSendIntervalMs)
            {
                if (!_client.SendFlag(flag)) { Fail(now); return; }
                _lastSentFlag = option;
                _lastFlagSentUtc = now;
                LogRequest("flag " + option);
            }

            if (safeGame != _lastSentGame
                && (now - _lastGameSentUtc).TotalMilliseconds >= settings.MinSendIntervalMs)
            {
                if (!_client.SendGame(safeGame)) { Fail(now); return; }
                _lastSentGame = safeGame;
                _lastGameSentUtc = now;
                LogRequest("game '" + safeGame + "'");
            }

            string mode = race ? "RACE" : "CLOCK";
            bool modeDue = mode != _lastSentRaceMode
                || (race && (now - _lastRaceModeSentUtc).TotalSeconds >= 10);
            if (modeDue)
            {
                if (!_client.SendRaceMode(race)) { Fail(now); return; }
                _lastSentRaceMode = mode;
                _lastRaceModeSentUtc = now;
                _lastSentTiming = null;
                _lastSentLogic = null;
                LogRequest("mode " + mode);
            }

            bool labelDue = lapLabel != _lastSentLapLabel
                || (race && (now - _lastLapLabelSentUtc).TotalSeconds >= 10);
            if (labelDue)
            {
                if (!_client.SendLapLabel(lapLabel)) { Fail(now); return; }
                _lastSentLapLabel = lapLabel;
                _lastLapLabelSentUtc = now;
                LogRequest("lap label " + lapLabel);
            }

            if (race)
            {
                bool timingDue = timing != _lastSentTiming
                    || (now - _lastTimingSentUtc).TotalSeconds >= 5;
                if (!string.IsNullOrEmpty(timing) && timingDue)
                {
                    if (!_client.SendTiming(timing)) { Fail(now); return; }
                    _lastSentTiming = timing;
                    _lastTimingSentUtc = now;
                    LogRequest("timing (" + timing.Length + " chars)");
                }
            }
            else if (_lastSentTiming != null)
            {
                // Clear the tower payload when leaving the race, so the next
                // race cannot flash a stale row before fresh data arrives.
                if (!_client.SendTiming(string.Empty)) { Fail(now); return; }
                _lastSentTiming = null;
                LogRequest("timing cleared");
            }

            if (race)
            {
                bool logicDue = logic != _lastSentLogic
                    || (now - _lastLogicSentUtc).TotalSeconds >= 5;
                if (!string.IsNullOrEmpty(logic) && logicDue)
                {
                    if (!_client.SendLogic(logic)) { Fail(now); return; }
                    _lastSentLogic = logic;
                    _lastLogicSentUtc = now;
                    LogRequest("logic (" + logic.Length + " chars)");
                }
            }
            else if (_lastSentLogic != null)
            {
                if (!_client.SendLogic(string.Empty)) { Fail(now); return; }
                _lastSentLogic = null;
                LogRequest("logic cleared");
            }

            // When "dim without game" is enabled, the backlight target is 0
            // until SimHub reports a running game; the slider value is then
            // restored on game start.
            int brightnessTarget = (settings.DimWhenNoGame && !gameRunning) ? 0 : settings.Brightness;
            bool brightnessDue = _brightnessDirty
                || (settings.DimWhenNoGame && brightnessTarget != _lastSentBrightness);
            if (brightnessDue)
            {
                if (!_client.SendBrightness(brightnessTarget)) { Fail(now); return; }
                _brightnessDirty = false;
                _lastSentBrightness = brightnessTarget;
                LogRequest("brightness " + brightnessTarget);
            }

            if ((now - _lastHeartbeatUtc).TotalMilliseconds >= settings.HeartbeatMs)
            {
                _heartbeatCounter++;
                // ESPHome numbers are floats internally; wrap before 2^24 so
                // every heartbeat still changes the displayed value and the
                // on_value trigger keeps firing.
                if (_heartbeatCounter > 1000000) _heartbeatCounter = 0;
                if (!_client.SendHeartbeat(_heartbeatCounter)) { Fail(now); return; }
                _lastHeartbeatUtc = now;
                LogRequest("heartbeat " + _heartbeatCounter);
            }

            SyncStatus(now);
        }

        private void Fail(DateTime now)
        {
            // Back off briefly so an unreachable display does not turn the
            // worker into a tight retry loop.
            _backoffUntilUtc = now.AddMilliseconds(750);
            SyncStatus(now);
        }

        private void SyncStatus(DateTime now)
        {
            bool wasReachable = _reachable;
            string error = _client != null ? _client.LastError : "no client";
            string url = _client != null ? _client.LastUrl : string.Empty;
            DateTime lastOk = _client != null ? _client.LastOkUtc : default(DateTime);

            lock (_stateLock)
            {
                _reachable = _client != null && _client.Reachable;
                _lastError = error ?? string.Empty;
                _lastUrl = url ?? string.Empty;
                _lastOkUtc = lastOk;
            }

            if (wasReachable != _reachable)
            {
                if (_reachable)
                    SimHub.Logging.Current.Info("CYD Flags: display connected at " + _lastUrl);
                else
                    SimHub.Logging.Current.Warn("CYD Flags: display not responding: " + _lastError);
            }

            if (!_reachable) LogErrorThrottled(_lastError, now);
        }

        private void LogErrorThrottled(string message, DateTime now)
        {
            if (string.Equals(message, _lastLoggedError, StringComparison.Ordinal)
                && (now - _lastErrorLogUtc).TotalSeconds < 10)
                return;

            _lastLoggedError = message;
            _lastErrorLogUtc = now;
            SimHub.Logging.Current.Warn("CYD Flags: " + message);
        }

        private void LogErrorThrottled(string message)
        {
            LogErrorThrottled(message, DateTime.UtcNow);
        }

        private void LogRequest(string what)
        {
            if (Settings != null && Settings.LogRequests)
                SimHub.Logging.Current.Info("CYD Flags: sent " + what);
        }

        private void SetGameRunning(bool running)
        {
            lock (_stateLock) _gameRunning = running;
        }

        private void SetState(FlagKind flag, string game, bool race, string timing, string logic, string lapLabel)
        {
            lock (_stateLock)
            {
                _flag = flag;
                _game = game ?? string.Empty;
                _raceMode = race;
                _timing = timing ?? string.Empty;
                _logic = logic ?? string.Empty;
                _lapLabel = lapLabel ?? "LAST";
            }
        }

        private static string SanitizeGame(string game)
        {
            if (string.IsNullOrEmpty(game)) return string.Empty;

            var sb = new StringBuilder(game.Length);
            foreach (char c in game.Trim())
            {
                if (c == '\r' || c == '\n' || c == '\t') sb.Append(' ');
                else if (!char.IsControl(c)) sb.Append(c);
            }

            string result = sb.ToString();
            if (result.Length > 60) result = result.Substring(0, 60);
            return result;
        }
    }
}
