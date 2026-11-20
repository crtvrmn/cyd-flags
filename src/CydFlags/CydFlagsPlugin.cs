using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using GameReaderCommon;
using SimHub.Plugins;

namespace CydFlags
{
    /// <summary>
    /// CYD Flags: pushes the current sim racing state to one or more Cheap
    /// Yellow Display panels running ESPHome, using the ESPHome web server
    /// REST API. No MQTT and no Home Assistant are required - just the
    /// displays on the same LAN.
    ///
    /// One worker thread talks to every enabled display in turn. Shared game
    /// state is computed once per frame; each display keeps its own page
    /// selection, brightness and connection state, so an offline panel only
    /// backs off that panel and never blocks the others.
    /// </summary>
    [PluginDescription("Shows flags, a clock and relative race timing on one or more ESP32 'Cheap Yellow Display' panels (ESPHome firmware over HTTP)")]
    [PluginAuthor("crt")]
    [PluginName("CYD Flags")]
    public class CydFlagsPlugin : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        public const string Version = "0.7.0";

        public CydFlagsSettings Settings;

        private readonly object _stateLock = new object();

        // Shared state, computed once by DataUpdate and read by every device.
        private FlagKind _flag = FlagKind.None;
        private string _game = string.Empty;
        private bool _raceMode;
        private bool _gameRunning;
        private string _timingLast = string.Empty;
        private string _timingBest = string.Empty;
        private string _logic = string.Empty;
        private string _delta = string.Empty;
        private string _rev = string.Empty;
        private string _lapLabelLast = "LAST";
        private string _lapLabelBest = "BEST";

        // Selected-display status mirror, used only for log transitions. The
        // public properties read the selected DeviceClient directly.
        private bool _reachable;
        private bool _wasGloballyEnabled = true;

        private readonly object _deviceLock = new object();
        private List<DeviceState> _deviceStates = new List<DeviceState>();
        private int _selectedDeviceIndex;

        private Thread _worker;
        private AutoResetEvent _wake;
        private volatile bool _stopping;

        private static readonly DateTime NoTiming = DateTime.MinValue;
        private DateTime _lastTimingBuildUtc = NoTiming;
        private DateTime _lastLogicBuildUtc = NoTiming;
        private DateTime _lastRevBuildUtc = NoTiming;
        private readonly DeltaTracker _deltaTracker = new DeltaTracker();

        // "Test flags" override: while set, DataUpdate ignores the game and
        // reports this flag so the user can check the panels without driving.
        private FlagKind _testFlag = FlagKind.None;
        private DateTime _testUntilUtc = DateTime.MinValue;

        // Log throttling.
        private string _lastLoggedError;
        private DateTime _lastErrorLogUtc;

        private double _lastDataErrorLogSeconds;
        private string _lastDataError;

        public PluginManager PluginManager { get; set; }

        public System.Windows.Media.ImageSource PictureIcon { get { return null; } }

        public string LeftMenuTitle { get { return "CYD Flags"; } }

        // ------------------------------------------------------------------
        // Read-only state for the settings UI and SimHub properties. Device
        // status follows the selected display; game values are shared.
        // ------------------------------------------------------------------
        public bool Linked
        {
            get
            {
                DeviceState state = SelectedState();
                return state != null && state.Client.Reachable;
            }
        }

        public string LastError
        {
            get
            {
                DeviceState state = SelectedState();
                return state != null ? state.Client.LastError : "no display configured";
            }
        }

        public string DeviceUrl
        {
            get
            {
                DeviceState state = SelectedState();
                if (state != null && !string.IsNullOrEmpty(state.Client.LastUrl))
                    return state.Client.LastUrl;
                return DescribeTarget();
            }
        }

        public DateTime LastReplyUtc
        {
            get
            {
                DeviceState state = SelectedState();
                return state != null ? state.Client.LastOkUtc : default(DateTime);
            }
        }

        public int RequestsSent
        {
            get
            {
                DeviceState state = SelectedState();
                return state != null ? state.Client.Requests : 0;
            }
        }

        public FlagKind CurrentFlag { get { lock (_stateLock) return _flag; } }
        public string CurrentGame { get { lock (_stateLock) return _game; } }
        public bool RaceMode { get { lock (_stateLock) return _raceMode; } }
        public string CurrentLogic { get { lock (_stateLock) return _logic; } }
        public string CurrentDelta { get { lock (_stateLock) return _delta; } }

        /// <summary>Timing payload for the selected display (LAST or BEST lap).</summary>
        public string CurrentTiming
        {
            get
            {
                bool best = SelectedDeviceUsesBestLap();
                lock (_stateLock) return best ? _timingBest : _timingLast;
            }
        }

        public int DeviceCount
        {
            get
            {
                lock (_deviceLock)
                {
                    CydFlagsSettings settings = Settings;
                    return settings != null && settings.Devices != null ? settings.Devices.Count : 0;
                }
            }
        }

        /// <summary>Index of the display whose settings the UI is editing.
        /// Clamped to the available devices.</summary>
        public int SelectedDeviceIndex
        {
            get
            {
                lock (_deviceLock)
                    return ClampSelectionLocked(_selectedDeviceIndex, DeviceCountLocked());
            }
            set
            {
                lock (_deviceLock)
                    _selectedDeviceIndex = ClampSelectionLocked(value, DeviceCountLocked());
            }
        }

        public CydFlagsDeviceSettings SelectedDevice
        {
            get
            {
                lock (_deviceLock)
                {
                    CydFlagsSettings settings = Settings;
                    if (settings == null || settings.Devices == null || settings.Devices.Count == 0)
                        return null;
                    int index = ClampSelectionLocked(_selectedDeviceIndex, settings.Devices.Count);
                    return settings.Devices[index];
                }
            }
        }

        public void Init(PluginManager pluginManager)
        {
            try
            {
                SimHub.Logging.Current.Info("CYD Flags " + Version + " initialising");

                Settings = this.ReadCommonSettings<CydFlagsSettings>("CydFlagsSettings", () => new CydFlagsSettings());
                if (Settings == null) Settings = new CydFlagsSettings();
                Settings.Clamp();
                SyncDeviceStates();

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

                SimHub.Logging.Current.Info("CYD Flags: device targets " + DescribeTargets());
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
                    SetRev(string.Empty);
                    SetDelta(string.Empty);
                    _deltaTracker.Reset();
                    _lastRevBuildUtc = NoTiming;
                    SetState(FlagKind.None, string.Empty, false,
                        string.Empty, string.Empty, string.Empty, "LAST", "BEST");
                    return;
                }

                bool testActive;
                FlagKind testFlag;
                lock (_stateLock)
                {
                    testActive = _testUntilUtc > DateTime.UtcNow;
                    testFlag = _testFlag;
                }
                if (testActive)
                {
                    // Treat a test as "game running" so the idle dim does not
                    // black out the panels while the user is checking flags.
                    SetGameRunning(true);
                    SetRev(string.Empty);
                    SetDelta(string.Empty);
                    _deltaTracker.Reset();
                    SetState(testFlag, string.Empty, false,
                        string.Empty, string.Empty, string.Empty, "LAST", "BEST");
                    _lastTimingBuildUtc = NoTiming;
                    _lastLogicBuildUtc = NoTiming;
                    _lastRevBuildUtc = NoTiming;
                    TimingBuilder.ResetTrend();
                    return;
                }

                if (!data.GameRunning || data.NewData == null)
                {
                    SetGameRunning(false);
                    SetRev(string.Empty);
                    SetDelta(string.Empty);
                    _deltaTracker.Reset();
                    SetState(FlagKind.None, string.Empty, false,
                        string.Empty, string.Empty, string.Empty, "LAST", "BEST");
                    _lastTimingBuildUtc = NoTiming;
                    _lastLogicBuildUtc = NoTiming;
                    _lastRevBuildUtc = NoTiming;
                    TimingBuilder.ResetTrend();
                    return;
                }

                SetGameRunning(true);

                // The game state is shared; each display only decides which
                // parts of it it wants to receive.
                CydFlagsDeviceSettings[] devices = DeviceSnapshot();
                bool anyFlags = false;
                bool anyTower = false;
                bool anyLogic = false;
                bool anyRev = false;
                bool anyDelta = false;
                foreach (CydFlagsDeviceSettings device in devices)
                {
                    if (device == null || !device.Enabled) continue;
                    if (device.PageFlags) anyFlags = true;
                    if (device.PageTower) anyTower = true;
                    if (device.PageLogic) anyLogic = true;
                    if (device.PageRev) anyRev = true;
                    if (device.PageTower && device.ShowDeltaBar) anyDelta = true;
                }

                FlagKind kind = anyFlags ? FlagReader.ReadKind(data.NewData) : FlagKind.None;
                string game = data.GameName ?? string.Empty;

                bool race = (anyTower || anyLogic) && TimingBuilder.IsRaceSession(data.NewData);
                string timingLast = string.Empty;
                string timingBest = string.Empty;
                string logicPayload = string.Empty;
                if (race)
                {
                    DateTime now = DateTime.UtcNow;
                    if (anyTower)
                    {
                        if ((now - _lastTimingBuildUtc).TotalMilliseconds >= 400)
                        {
                            // Build both variants so each display can show its
                            // own LAST/BEST choice from the shared snapshot.
                            // BuildBoth updates the shared gap trend only once.
                            TimingBuilder.BuildBoth(data.NewData, out timingLast, out timingBest);
                            _lastTimingBuildUtc = now;
                        }
                        else
                        {
                            lock (_stateLock)
                            {
                                timingLast = _timingLast;
                                timingBest = _timingBest;
                            }
                        }
                    }
                    else
                    {
                        _lastTimingBuildUtc = NoTiming;
                    }

                    if (anyLogic)
                    {
                        if ((now - _lastLogicBuildUtc).TotalMilliseconds >= 400)
                        {
                            logicPayload = RaceLogicBuilder.Build(data.NewData);
                            _lastLogicBuildUtc = now;
                        }
                        else
                        {
                            lock (_stateLock) logicPayload = _logic;
                        }
                    }
                    else
                    {
                        _lastLogicBuildUtc = NoTiming;
                    }
                }
                else
                {
                    _lastTimingBuildUtc = NoTiming;
                    _lastLogicBuildUtc = NoTiming;
                    TimingBuilder.ResetTrend();
                }

                string revPayload = string.Empty;
                DateTime revNow = DateTime.UtcNow;
                if (anyRev)
                {
                    if ((revNow - _lastRevBuildUtc).TotalMilliseconds >= 100)
                    {
                        revPayload = RevBuilder.Build(data.NewData);
                        _lastRevBuildUtc = revNow;
                    }
                    else
                    {
                        lock (_stateLock) revPayload = _rev;
                    }
                }
                else
                {
                    _lastRevBuildUtc = NoTiming;
                }
                SetRev(revPayload);

                string delta = string.Empty;
                if (race && anyDelta)
                {
                    DeltaResult result = _deltaTracker.Update(data.NewData);
                    if (result.HasValue)
                    {
                        double seconds = result.Seconds;
                        string sign = seconds < -0.0005 ? "-" : "+";
                        delta = sign + Math.Abs(seconds).ToString("0.000", CultureInfo.InvariantCulture);
                    }
                }
                else
                {
                    _deltaTracker.Reset();
                }
                SetDelta(delta);

                bool pit = Convert.ToBoolean(data.NewData.IsInPitLane);
                string suffix = pit ? " PIT" : "";
                SetState(kind, game, race, timingLast, timingBest, logicPayload,
                    "LAST" + suffix, "BEST" + suffix);
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
            SyncDeviceStates();

            DeviceState[] states;
            lock (_deviceLock) states = _deviceStates.ToArray();
            foreach (DeviceState state in states)
            {
                if (state.Settings.ApplyBrightness || state.Settings.DimWhenNoGame)
                    state.BrightnessDirty = true;
            }

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

        /// <summary>Add a display and select it. The UI calls ApplySettings first.</summary>
        public CydFlagsDeviceSettings AddDevice()
        {
            CydFlagsDeviceSettings device;
            lock (_deviceLock)
            {
                CydFlagsSettings settings = Settings;
                if (settings == null) return null;
                device = settings.AddDevice();
                _selectedDeviceIndex = settings.Devices.Count - 1;
            }

            ApplySettings();
            return device;
        }

        /// <summary>Remove the selected display. The last display cannot be removed.</summary>
        public bool RemoveSelectedDevice()
        {
            bool removed = false;
            lock (_deviceLock)
            {
                CydFlagsSettings settings = Settings;
                if (settings != null && settings.Devices != null && settings.Devices.Count > 1)
                {
                    int index = ClampSelectionLocked(_selectedDeviceIndex, settings.Devices.Count);
                    settings.Devices.RemoveAt(index);
                    _selectedDeviceIndex = ClampSelectionLocked(index, settings.Devices.Count);
                    removed = true;
                }
            }

            if (removed) ApplySettings();
            return removed;
        }

        /// <summary>Force a fresh flag/game/heartbeat push on every display,
        /// used by the Test button.</summary>
        public void TestConnection()
        {
            DeviceState[] states;
            lock (_deviceLock) states = _deviceStates.ToArray();
            foreach (DeviceState state in states)
            {
                ResetSendState(state);
                state.BrightnessDirty = true;
            }
            if (_wake != null) _wake.Set();
        }

        /// <summary>Push the brightness slider value of the selected display now.</summary>
        public void ApplyBrightnessNow()
        {
            DeviceState state = SelectedState();
            if (state != null)
            {
                state.BrightnessDirty = true;
                state.BackoffUntilUtc = DateTime.MinValue;
            }
            if (_wake != null) _wake.Set();
        }

        /// <summary>Start showing a test flag for a few seconds on all enabled
        /// displays, ignoring the game. Used by the settings panel to verify
        /// the panels.</summary>
        public void ShowTestFlag(FlagKind kind, int seconds)
        {
            if (seconds < 1) seconds = 1;
            if (seconds > 60) seconds = 60;
            lock (_stateLock)
            {
                _testFlag = kind;
                _testUntilUtc = DateTime.UtcNow.AddSeconds(seconds);
            }
            if (_wake != null) _wake.Set();
        }

        public void ClearTestFlag()
        {
            lock (_stateLock) _testUntilUtc = DateTime.MinValue;
            if (_wake != null) _wake.Set();
        }

        /// <summary>Write WiFi credentials into one ESP's NVS and connect.
        /// Returns an empty string on success, otherwise a user-facing error.
        /// A full USB/factory flash erases NVS again; OTA and reboots keep it.
        /// The password is only sent to the display, never saved in settings.</summary>
        public string SaveWifiToDisplay(CydFlagsDeviceSettings device, string ssid, string password)
        {
            if (device == null) return "No display selected.";

            DeviceState state = FindState(device);
            if (state == null)
            {
                // A display may have just been added and not reached the
                // worker yet; refresh the runtime states once.
                SyncDeviceStates();
                state = FindState(device);
            }
            if (state == null) return "Plugin is not initialised yet.";
            if (string.IsNullOrWhiteSpace(ssid)) return "SSID must not be empty.";

            string cleanSsid = ssid.Trim();
            if (!state.Client.SendWifiSsid(cleanSsid))
                return "SSID: " + (state.Client.LastError ?? "request failed");
            if (!state.Client.SendWifiPassword(password ?? string.Empty))
                return "Password: " + (state.Client.LastError ?? "request failed");
            if (!state.Client.SendWifiConnect())
                return "Connect: " + (state.Client.LastError ?? "request failed");

            device.WifiSsid = cleanSsid;
            ApplySettings();
            SyncStatus(DateTime.UtcNow);
            return string.Empty;
        }

        /// <summary>Backwards-compatible helper: save WiFi for the selected display.</summary>
        public string SaveWifiToDisplay(string ssid, string password)
        {
            return SaveWifiToDisplay(SelectedDevice, ssid, password);
        }

        public string DescribeTarget()
        {
            return DescribeTarget(SelectedDevice);
        }

        public string DescribeTarget(CydFlagsDeviceSettings device)
        {
            if (device == null) return "-";
            return "http://" + device.Host + ":" + device.Port;
        }

        public string DeviceWebUrl
        {
            get { return DescribeTarget() + "/"; }
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
            if (settings == null) return;

            DateTime now = DateTime.UtcNow;

            DeviceState[] states;
            lock (_deviceLock) states = _deviceStates.ToArray();

            if (!settings.Enabled)
            {
                // Forget what was sent while the plugin was switched off, so
                // re-enabling pushes a complete state instead of resuming a
                // stale cache (the panels have shown NO LINK in the meantime).
                if (_wasGloballyEnabled)
                {
                    _wasGloballyEnabled = false;
                    foreach (DeviceState state in states)
                    {
                        if (state == null) continue;
                        ResetSendState(state);
                        state.WasEnabled = state.Settings != null && state.Settings.Enabled;
                        state.BrightnessDirty = state.Settings != null
                            && (state.Settings.ApplyBrightness || state.Settings.DimWhenNoGame);
                    }
                }
                return;
            }
            _wasGloballyEnabled = true;

            if (states.Length == 0) return;

            // Snapshot the shared frame once. Every device uses the same flag,
            // game, race mode and timing; only page gates and presentation
            // values differ.
            SharedState shared = ReadSharedState();

            foreach (DeviceState state in states)
            {
                if (state == null || state.Settings == null) continue;

                if (!state.Settings.Enabled)
                {
                    state.WasEnabled = false;
                    continue;
                }

                if (!state.WasEnabled)
                {
                    // A display that was just enabled gets a fresh start.
                    state.WasEnabled = true;
                    ResetSendState(state);
                    state.BrightnessDirty =
                        state.Settings.ApplyBrightness || state.Settings.DimWhenNoGame;
                }

                try
                {
                    PumpDevice(settings, state, shared, now);
                }
                catch (Exception ex)
                {
                    // One broken display must not stop the others.
                    state.BackoffUntilUtc = now.AddMilliseconds(750);
                    LogErrorThrottled("[" + state.Settings.Name + "] " + ex.Message, now);
                }
            }

            SyncStatus(now);
        }

        private void PumpDevice(CydFlagsSettings settings, DeviceState state, SharedState shared, DateTime now)
        {
            CydFlagsDeviceSettings device = state.Settings;

            string hostKey = BuildHostKey(device);
            if (hostKey != state.HostKey)
            {
                state.HostKey = hostKey;
                ResetSendState(state);
                state.BrightnessDirty = device.ApplyBrightness || device.DimWhenNoGame;
            }

            if (now < state.BackoffUntilUtc) return;

            // Make sure a game start always re-sends the slider brightness,
            // even if the previous target happened to be the same value. This
            // is what restores the panel after the idle 0% dim.
            if (shared.GameRunning != state.LastGameRunning)
            {
                state.LastGameRunning = shared.GameRunning;
                state.BrightnessDirty = true;
            }

            string option = FlagModel.ToOption(shared.Flag);
            string safeGame = SanitizeGame(shared.Game);

            // Page feature list. Sent first so a freshly booted display hides
            // pages before the first flag/game payload arrives.
            string features = device.FeatureString();
            bool featuresDue = features != state.LastSentFeatures
                || (now - state.LastFeaturesSentUtc).TotalSeconds >= 10;
            if (featuresDue)
            {
                if (!state.Client.SendFeatures(features)) { Fail(state, now); return; }
                state.LastSentFeatures = features;
                state.LastFeaturesSentUtc = now;
                LogRequest(state, "features " + features);
            }

            string orientation = device.Orientation ?? "portrait";
            bool orientationDue = orientation != state.LastSentOrientation
                || (now - state.LastOrientationSentUtc).TotalSeconds >= 10;
            if (orientationDue)
            {
                // Older firmware returns 404 here; do not stop flags/timing for
                // the whole display just because it predates rotation support.
                if (state.Client.SendOrientation(orientation))
                    LogRequest(state, "orientation " + orientation);
                else
                    LogRequest(state, "orientation not accepted (older firmware?)");
                state.LastSentOrientation = orientation;
                state.LastOrientationSentUtc = now;
            }

            if (option != state.LastSentFlag
                && (now - state.LastFlagSentUtc).TotalMilliseconds >= settings.MinSendIntervalMs)
            {
                if (!state.Client.SendFlag(shared.Flag)) { Fail(state, now); return; }
                state.LastSentFlag = option;
                state.LastFlagSentUtc = now;
                LogRequest(state, "flag " + option);
            }

            if (safeGame != state.LastSentGame
                && (now - state.LastGameSentUtc).TotalMilliseconds >= settings.MinSendIntervalMs)
            {
                if (!state.Client.SendGame(safeGame)) { Fail(state, now); return; }
                state.LastSentGame = safeGame;
                state.LastGameSentUtc = now;
                LogRequest(state, "game '" + safeGame + "'");
            }

            string mode = shared.Race ? "RACE" : "CLOCK";
            bool modeDue = mode != state.LastSentRaceMode
                || (shared.Race && (now - state.LastRaceModeSentUtc).TotalSeconds >= 10);
            if (modeDue)
            {
                if (!state.Client.SendRaceMode(shared.Race)) { Fail(state, now); return; }
                state.LastSentRaceMode = mode;
                state.LastRaceModeSentUtc = now;
                state.LastSentTiming = null;
                state.LastSentLogic = null;
                LogRequest(state, "mode " + mode);
            }

            string lapLabel = device.ShowBestLap ? shared.LapLabelBest : shared.LapLabelLast;
            bool labelDue = lapLabel != state.LastSentLapLabel
                || (shared.Race && (now - state.LastLapLabelSentUtc).TotalSeconds >= 10);
            if (labelDue)
            {
                if (!state.Client.SendLapLabel(lapLabel)) { Fail(state, now); return; }
                state.LastSentLapLabel = lapLabel;
                state.LastLapLabelSentUtc = now;
                LogRequest(state, "lap label " + lapLabel);
            }

            if (shared.Race && device.PageTower)
            {
                string timing = device.ShowBestLap ? shared.TimingBest : shared.TimingLast;
                bool timingDue = timing != state.LastSentTiming
                    || (now - state.LastTimingSentUtc).TotalSeconds >= 5;
                if (!string.IsNullOrEmpty(timing) && timingDue)
                {
                    if (!state.Client.SendTiming(timing)) { Fail(state, now); return; }
                    state.LastSentTiming = timing;
                    state.LastTimingSentUtc = now;
                    LogRequest(state, "timing (" + timing.Length + " chars)");
                }
            }
            else if (state.LastSentTiming != null)
            {
                // Clear the tower payload when leaving the race or when the
                // tower page is disabled, so no stale rows can appear later.
                if (!state.Client.SendTiming(string.Empty)) { Fail(state, now); return; }
                state.LastSentTiming = null;
                LogRequest(state, "timing cleared");
            }

            if (shared.Race && device.PageTower && device.ShowDeltaBar
                && !string.IsNullOrEmpty(shared.Delta))
            {
                bool deltaDue = shared.Delta != state.LastSentDelta
                    || (now - state.LastDeltaSentUtc).TotalSeconds >= 1;
                if (deltaDue)
                {
                    if (!state.Client.SendDelta(shared.Delta)) { Fail(state, now); return; }
                    state.LastSentDelta = shared.Delta;
                    state.LastDeltaSentUtc = now;
                    LogRequest(state, "delta " + shared.Delta);
                }
            }
            else if (state.LastSentDelta != null)
            {
                if (!state.Client.SendDelta(string.Empty)) { Fail(state, now); return; }
                state.LastSentDelta = null;
                LogRequest(state, "delta cleared");
            }

            if (shared.Race && device.PageLogic)
            {
                bool logicDue = shared.Logic != state.LastSentLogic
                    || (now - state.LastLogicSentUtc).TotalSeconds >= 5;
                if (!string.IsNullOrEmpty(shared.Logic) && logicDue)
                {
                    if (!state.Client.SendLogic(shared.Logic)) { Fail(state, now); return; }
                    state.LastSentLogic = shared.Logic;
                    state.LastLogicSentUtc = now;
                    LogRequest(state, "logic (" + shared.Logic.Length + " chars)");
                }
            }
            else if (state.LastSentLogic != null)
            {
                if (!state.Client.SendLogic(string.Empty)) { Fail(state, now); return; }
                state.LastSentLogic = null;
                LogRequest(state, "logic cleared");
            }

            if (device.PageRev && !string.IsNullOrEmpty(shared.Rev))
            {
                bool revDue = shared.Rev != state.LastSentRev
                    || (now - state.LastRevSentUtc).TotalMilliseconds >= 1000;
                if (revDue)
                {
                    if (!state.Client.SendRev(shared.Rev)) { Fail(state, now); return; }
                    state.LastSentRev = shared.Rev;
                    state.LastRevSentUtc = now;
                    LogRequest(state, "rev " + shared.Rev);
                }
            }
            else if (state.LastSentRev != null)
            {
                if (!state.Client.SendRev(string.Empty)) { Fail(state, now); return; }
                state.LastSentRev = null;
                LogRequest(state, "rev cleared");
            }

            // When "dim without game" is enabled, the backlight target is 0
            // until SimHub reports a running game; the slider value is then
            // restored on game start.
            int brightnessTarget = (device.DimWhenNoGame && !shared.GameRunning) ? 0 : device.Brightness;
            bool brightnessDue = state.BrightnessDirty
                || (device.DimWhenNoGame && brightnessTarget != state.LastSentBrightness);
            if (brightnessDue)
            {
                if (!state.Client.SendBrightness(brightnessTarget)) { Fail(state, now); return; }
                state.BrightnessDirty = false;
                state.LastSentBrightness = brightnessTarget;
                LogRequest(state, "brightness " + brightnessTarget);
            }

            if ((now - state.LastHeartbeatUtc).TotalMilliseconds >= settings.HeartbeatMs)
            {
                state.HeartbeatCounter++;
                // ESPHome numbers are floats internally; wrap before 2^24 so
                // every heartbeat still changes the displayed value and the
                // on_value trigger keeps firing.
                if (state.HeartbeatCounter > 1000000) state.HeartbeatCounter = 0;
                if (!state.Client.SendHeartbeat(state.HeartbeatCounter)) { Fail(state, now); return; }
                state.LastHeartbeatUtc = now;
                LogRequest(state, "heartbeat " + state.HeartbeatCounter);
            }
        }

        private void Fail(DeviceState state, DateTime now)
        {
            // Back off only this display so an unreachable panel does not turn
            // the worker into a tight retry loop and does not delay the others.
            state.BackoffUntilUtc = now.AddMilliseconds(750);
        }

        private void SyncStatus(DateTime now)
        {
            DeviceState state = SelectedState();
            bool reachable = state != null && state.Client.Reachable;
            string error = state != null ? (state.Client.LastError ?? string.Empty) : "no display configured";
            string url = state != null ? (state.Client.LastUrl ?? string.Empty) : string.Empty;

            bool wasReachable;
            lock (_stateLock)
            {
                wasReachable = _reachable;
                _reachable = reachable;
            }

            if (wasReachable != reachable)
            {
                if (reachable)
                    SimHub.Logging.Current.Info("CYD Flags: display connected at " + url);
                else
                    SimHub.Logging.Current.Warn("CYD Flags: display not responding: " + error);
            }

            if (!reachable && !string.IsNullOrEmpty(error)) LogErrorThrottled(error, now);
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

        private void LogRequest(DeviceState state, string what)
        {
            if (Settings != null && Settings.LogRequests)
                SimHub.Logging.Current.Info("CYD Flags [" + state.Settings.Name + "]: sent " + what);
        }

        private void SetGameRunning(bool running)
        {
            lock (_stateLock) _gameRunning = running;
        }

        private void SetState(FlagKind flag, string game, bool race,
            string timingLast, string timingBest, string logic,
            string lapLabelLast, string lapLabelBest)
        {
            lock (_stateLock)
            {
                _flag = flag;
                _game = game ?? string.Empty;
                _raceMode = race;
                _timingLast = timingLast ?? string.Empty;
                _timingBest = timingBest ?? string.Empty;
                _logic = logic ?? string.Empty;
                _lapLabelLast = lapLabelLast ?? "LAST";
                _lapLabelBest = lapLabelBest ?? "BEST";
            }
        }

        private void SetRev(string rev)
        {
            lock (_stateLock) _rev = rev ?? string.Empty;
        }

        private void SetDelta(string delta)
        {
            lock (_stateLock) _delta = delta ?? string.Empty;
        }

        private SharedState ReadSharedState()
        {
            var state = new SharedState();
            lock (_stateLock)
            {
                state.Flag = _flag;
                state.Game = _game;
                state.Race = _raceMode;
                state.GameRunning = _gameRunning;
                state.TimingLast = _timingLast;
                state.TimingBest = _timingBest;
                state.Logic = _logic;
                state.Rev = _rev;
                state.Delta = _delta;
                state.LapLabelLast = _lapLabelLast;
                state.LapLabelBest = _lapLabelBest;
            }
            return state;
        }

        // ------------------------------------------------------------------
        // Per-device runtime state
        // ------------------------------------------------------------------
        private sealed class DeviceState
        {
            public readonly CydFlagsDeviceSettings Settings;
            public readonly DeviceClient Client;

            public string LastSentFlag;
            public string LastSentGame;
            public string LastSentRaceMode;
            public string LastSentTiming;
            public string LastSentLogic;
            public string LastSentLapLabel;
            public string LastSentFeatures;
            public string LastSentRev;
            public string LastSentDelta;
            public string LastSentOrientation;

            public string HostKey;
            public bool WasEnabled;

            public DateTime LastFlagSentUtc;
            public DateTime LastGameSentUtc;
            public DateTime LastHeartbeatUtc;
            public DateTime LastRaceModeSentUtc;
            public DateTime LastTimingSentUtc;
            public DateTime LastLogicSentUtc;
            public DateTime LastLapLabelSentUtc;
            public DateTime LastFeaturesSentUtc;
            public DateTime LastRevSentUtc;
            public DateTime LastDeltaSentUtc;
            public DateTime LastOrientationSentUtc;

            public DateTime BackoffUntilUtc;
            public int HeartbeatCounter;
            public bool BrightnessDirty;
            public int LastSentBrightness = -1;
            public bool LastGameRunning;

            public DeviceState(CydFlagsDeviceSettings settings)
            {
                Settings = settings;
                Client = new DeviceClient(() => Settings);
                WasEnabled = settings.Enabled;
                HostKey = BuildHostKey(settings);
            }
        }

        private sealed class SharedState
        {
            public FlagKind Flag;
            public string Game;
            public bool Race;
            public bool GameRunning;
            public string TimingLast;
            public string TimingBest;
            public string Logic;
            public string Rev;
            public string Delta;
            public string LapLabelLast;
            public string LapLabelBest;
        }

        private void SyncDeviceStates()
        {
            CydFlagsSettings settings = Settings;
            if (settings == null) return;

            lock (_deviceLock)
            {
                if (settings.Devices == null)
                    settings.Devices = new List<CydFlagsDeviceSettings>();

                var states = new List<DeviceState>();
                foreach (CydFlagsDeviceSettings device in settings.Devices)
                {
                    if (device == null) continue;
                    DeviceState state = FindStateLocked(device);
                    if (state == null)
                    {
                        state = new DeviceState(device);
                        state.BrightnessDirty =
                            device.ApplyBrightness || device.DimWhenNoGame;
                    }
                    states.Add(state);
                }

                _deviceStates = states;
                _selectedDeviceIndex = ClampSelectionLocked(_selectedDeviceIndex, states.Count);
            }
        }

        private DeviceState FindState(CydFlagsDeviceSettings device)
        {
            if (device == null) return null;
            lock (_deviceLock)
            {
                foreach (DeviceState state in _deviceStates)
                {
                    if (state != null && ReferenceEquals(state.Settings, device))
                        return state;
                }
            }
            return null;
        }

        private DeviceState FindStateLocked(CydFlagsDeviceSettings device)
        {
            foreach (DeviceState state in _deviceStates)
            {
                if (state != null && ReferenceEquals(state.Settings, device))
                    return state;
            }
            return null;
        }

        private DeviceState SelectedState()
        {
            lock (_deviceLock)
            {
                if (_deviceStates == null || _deviceStates.Count == 0) return null;
                int index = ClampSelectionLocked(_selectedDeviceIndex, _deviceStates.Count);
                return _deviceStates[index];
            }
        }

        private bool SelectedDeviceUsesBestLap()
        {
            CydFlagsDeviceSettings device = SelectedDevice;
            return device != null && device.ShowBestLap;
        }

        private CydFlagsDeviceSettings[] DeviceSnapshot()
        {
            CydFlagsSettings settings = Settings;
            if (settings == null) return new CydFlagsDeviceSettings[0];
            lock (_deviceLock)
            {
                if (settings.Devices == null) return new CydFlagsDeviceSettings[0];
                return settings.Devices.ToArray();
            }
        }

        private int DeviceCountLocked()
        {
            CydFlagsSettings settings = Settings;
            if (settings == null || settings.Devices == null) return 0;
            return settings.Devices.Count;
        }

        private static int ClampSelectionLocked(int index, int count)
        {
            if (count <= 0) return 0;
            if (index < 0) return 0;
            if (index >= count) return count - 1;
            return index;
        }

        private static void ResetSendState(DeviceState state)
        {
            state.LastSentFlag = null;
            state.LastSentGame = null;
            state.LastSentRaceMode = null;
            state.LastSentTiming = null;
            state.LastSentLogic = null;
            state.LastSentLapLabel = null;
            state.LastSentRev = null;
            state.LastSentDelta = null;
            state.LastSentOrientation = null;
            state.LastSentFeatures = null;
            state.LastSentBrightness = -1;
            state.LastHeartbeatUtc = DateTime.MinValue;
            state.LastRaceModeSentUtc = DateTime.MinValue;
            state.LastTimingSentUtc = DateTime.MinValue;
            state.LastLogicSentUtc = DateTime.MinValue;
            state.LastLapLabelSentUtc = DateTime.MinValue;
            state.LastRevSentUtc = DateTime.MinValue;
            state.LastOrientationSentUtc = DateTime.MinValue;
            state.LastFeaturesSentUtc = DateTime.MinValue;
            state.LastFlagSentUtc = DateTime.MinValue;
            state.LastGameSentUtc = DateTime.MinValue;
            state.BackoffUntilUtc = DateTime.MinValue;
            state.Client.ResetStatus();
        }

        private static string BuildHostKey(CydFlagsDeviceSettings device)
        {
            return (device.Host ?? string.Empty) + "|" + device.Port;
        }

        private string DescribeTargets()
        {
            var sb = new StringBuilder();
            foreach (CydFlagsDeviceSettings device in DeviceSnapshot())
            {
                if (device == null) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(device.Name).Append(" @ ").Append(DescribeTarget(device));
            }
            return sb.Length == 0 ? "-" : sb.ToString();
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
