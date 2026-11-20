using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CydFlags.UI
{
    /// <summary>
    /// Settings panel. Built in code rather than XAML on purpose: the project
    /// then compiles with a plain C# compiler (no XamlBuildTask, no generated
    /// .g.cs), which makes it easy to build and verify outside Visual Studio.
    ///
    /// 0.7.0 supports several displays. The selector at the top decides which
    /// display the device-specific controls edit; the game state itself is
    /// shared by every enabled display.
    /// </summary>
    public sealed class CydFlagsSettingsView : UserControl
    {
        private readonly CydFlagsPlugin _plugin;
        private readonly DispatcherTimer _timer;

        private TextBlock _linkLine;
        private TextBlock _detailLine;

        // Global settings.
        private CheckBox _enabled;
        private TextBox _heartbeat;
        private TextBox _minInterval;
        private CheckBox _logRequests;

        // Device selection.
        private ComboBox _deviceCombo;
        private Button _removeDevice;

        // Selected-display settings.
        private CydFlagsDeviceSettings _device;
        private CheckBox _deviceEnabled;
        private TextBox _deviceName;
        private TextBox _host;
        private TextBox _port;
        private CheckBox _showBestLap;
        private CheckBox _applyBrightness;
        private CheckBox _dimWhenNoGame;
        private Slider _brightness;
        private CheckBox _pageFlags;
        private CheckBox _pageClock;
        private CheckBox _pageTower;
        private CheckBox _pageLogic;
        private TextBox _wifiSsid;
        private PasswordBox _wifiPassword;
        private TextBlock _wifiStatus;

        private ComboBox _testFlag;
        private TextBlock _testStatus;

        private bool _dirty;
        private DateTime _lastApplyUtc;
        private bool _building;
        private bool _updatingSelection;

        public CydFlagsSettingsView(CydFlagsPlugin plugin)
        {
            _plugin = plugin;
            Build();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += (s, e) => Tick();
            _timer.Start();
            Loaded += (s, e) => { if (!_timer.IsEnabled) _timer.Start(); };
            Unloaded += (s, e) => _timer.Stop();
            RefreshStatus();
        }

        private CydFlagsSettings Settings { get { return _plugin.Settings; } }

        private void Build()
        {
            _building = true;
            try
            {
                CydFlagsSettings settings = Settings;
                if (settings == null)
                {
                    Content = new TextBlock { Text = "CYD Flags is not initialised yet.", Margin = new Thickness(12) };
                    return;
                }

                settings.Clamp();
                CydFlagsDeviceSettings[] devices = settings.Devices.ToArray();
                int selected = _plugin.SelectedDeviceIndex;
                if (selected < 0 || selected >= devices.Length) selected = 0;
                _plugin.SelectedDeviceIndex = selected;
                _device = devices.Length > 0 ? devices[selected] : null;
                CydFlagsDeviceSettings device = _device;

                var root = new StackPanel { Margin = new Thickness(12) };

                root.Children.Add(new TextBlock
                {
                    Text = "CYD Flags " + CydFlagsPlugin.Version,
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 4)
                });

                root.Children.Add(new TextBlock
                {
                    Text = "Shows sim racing flags on one or more Cheap Yellow Display (ESP32-2432S028) panels " +
                           "running the cyd-flags ESPHome firmware. SimHub pushes each change to every enabled " +
                           "display's built-in web server - no MQTT and no Home Assistant needed.\n\n" +
                           "Add the displays below and pick one to edit. The same race data goes to all of them; " +
                           "pages, brightness and WiFi are configured per display.",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 660,
                    Margin = new Thickness(0, 0, 0, 8)
                });

                _linkLine = new TextBlock
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 2)
                };
                root.Children.Add(_linkLine);

                _detailLine = new TextBlock
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                root.Children.Add(_detailLine);

                // ---------------- global plugin settings -----------------
                AddSection(root, "Plugin");
                _enabled = AddCheck(root, "Enable CYD Flags (all displays)",
                    () => Settings.Enabled, v => Settings.Enabled = v);
                _heartbeat = AddText(root, "Heartbeat interval (ms)",
                    () => Settings.HeartbeatMs.ToString(CultureInfo.InvariantCulture),
                    v => Settings.HeartbeatMs = ParseInt(v, Settings.HeartbeatMs), 70);
                _minInterval = AddText(root, "Minimum gap between flag packets (ms)",
                    () => Settings.MinSendIntervalMs.ToString(CultureInfo.InvariantCulture),
                    v => Settings.MinSendIntervalMs = ParseInt(v, Settings.MinSendIntervalMs), 70);

                root.Children.Add(new TextBlock
                {
                    Text = "The heartbeat tells each display that the PC is still alive; without one it shows " +
                           "NO LINK after a few seconds. A small gap between flag packets avoids flicker during " +
                           "rapid flag changes.",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 620,
                    FontSize = 11,
                    Opacity = 0.75,
                    Margin = new Thickness(0, 2, 0, 4)
                });

                // ---------------- device selection -----------------------
                AddSection(root, "Displays");
                var selector = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 3, 0, 3)
                };
                selector.Children.Add(new TextBlock
                {
                    Text = "Configured display",
                    Width = 260,
                    VerticalAlignment = VerticalAlignment.Center
                });

                _deviceCombo = new ComboBox
                {
                    Width = 220,
                    VerticalAlignment = VerticalAlignment.Center
                };
                foreach (CydFlagsDeviceSettings entry in devices)
                    _deviceCombo.Items.Add(entry.Name);

                _updatingSelection = true;
                _deviceCombo.SelectedIndex = selected;
                _updatingSelection = false;
                _deviceCombo.SelectionChanged += OnDeviceSelected;
                selector.Children.Add(_deviceCombo);

                Button add = NewButton("Add device", 8);
                add.Click += (s, e) =>
                {
                    Apply();
                    _plugin.AddDevice();
                    Build();
                };
                selector.Children.Add(add);

                _removeDevice = NewButton("Remove device", 8);
                _removeDevice.IsEnabled = devices.Length > 1;
                _removeDevice.Click += (s, e) =>
                {
                    Apply();
                    if (!_plugin.RemoveSelectedDevice())
                    {
                        MessageBox.Show("At least one display must remain.",
                            "CYD Flags", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    Build();
                };
                selector.Children.Add(_removeDevice);
                root.Children.Add(selector);

                root.Children.Add(new TextBlock
                {
                    Text = "Each display has its own address, pages and backlight. The settings below always " +
                           "belong to the display selected above.",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 620,
                    FontSize = 11,
                    Opacity = 0.75,
                    Margin = new Thickness(0, 2, 0, 4)
                });

                if (device == null)
                {
                    root.Children.Add(new TextBlock
                    {
                        Text = "No display configured. Use Add device.",
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 8, 0, 8)
                    });
                }
                else
                {
                    BuildDeviceSection(root, device);
                }

                // ---------------- flag test (all displays) ----------------
                AddSection(root, "Test the flags (all displays)");
                var testPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
                testPanel.Children.Add(new TextBlock
                {
                    Text = "Flag",
                    Width = 260,
                    VerticalAlignment = VerticalAlignment.Center
                });
                _testFlag = new ComboBox { Width = 180 };
                foreach (FlagKind kind in new[]
                {
                    FlagKind.None, FlagKind.Green, FlagKind.Yellow, FlagKind.Blue,
                    FlagKind.White, FlagKind.Checkered, FlagKind.Red, FlagKind.Black, FlagKind.Orange
                })
                {
                    _testFlag.Items.Add(FlagModel.ToOption(kind));
                }
                _testFlag.SelectedItem = "YELLOW";
                testPanel.Children.Add(_testFlag);
                root.Children.Add(testPanel);

                Button sendTest = AddButton(root, "Show on all displays for 10 seconds");
                sendTest.Click += (s, e) => ShowTestFlag();
                Button stopTest = AddButton(root, "Stop test");
                stopTest.Click += (s, e) =>
                {
                    _plugin.ClearTestFlag();
                    if (_testStatus != null) _testStatus.Text = "Test stopped; normal SimHub data resumes.";
                };

                _testStatus = new TextBlock
                {
                    Text = "The test overrides the game for 10 seconds on every enabled display, so you can check " +
                           "each flag without driving.",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 620,
                    FontSize = 11,
                    Opacity = 0.75,
                    Margin = new Thickness(0, 2, 0, 4)
                };
                root.Children.Add(_testStatus);

                // ---------------- diagnostics -----------------------------
                AddSection(root, "Diagnostics");
                _logRequests = AddCheck(root, "Log every HTTP request (chatty)",
                    () => Settings.LogRequests, v => Settings.LogRequests = v);

                Button test = AddButton(root, "Test connection (all displays)");
                test.Click += (s, e) =>
                {
                    Apply();
                    _plugin.TestConnection();
                    RefreshStatus();
                };

                root.Children.Add(new TextBlock
                {
                    Text = "SimHub properties: CydFlagsLinked, CydFlagsFlag, CydFlagsGame, CydFlagsDevice, CydFlagsError " +
                           "(status follows the selected display). Action: CydFlagsTest. Log lines are prefixed with \"CYD Flags\".",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 620,
                    FontSize = 11,
                    Opacity = 0.75,
                    Margin = new Thickness(0, 12, 0, 0)
                });

                Content = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = root
                };
            }
            finally
            {
                _building = false;
            }
        }

        private void BuildDeviceSection(Panel root, CydFlagsDeviceSettings device)
        {
            AddSection(root, "Display: " + device.Name);

            _deviceEnabled = AddCheck(root, "Enable this display",
                () => device.Enabled, v => device.Enabled = v);
            _deviceName = AddText(root, "Name", () => device.Name, v =>
            {
                device.Name = v;
                UpdateComboName();
            }, 300);

            _host = AddText(root, "Host or IP", () => device.Host, v => device.Host = v, 300);
            _port = AddText(root, "Port", () => device.Port.ToString(CultureInfo.InvariantCulture),
                v =>
                {
                    int port;
                    if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
                        device.Port = port;
                }, 70);

            Button open = AddButton(root, "Open display web UI");
            open.Click += (s, e) =>
            {
                Apply();
                try
                {
                    Process.Start(new ProcessStartInfo(_plugin.DeviceWebUrl) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not open the display web UI:\n" + ex.Message,
                        "CYD Flags", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            AddSection(root, "Pages on this display");
            _pageFlags = AddCheck(root, "Flags", () => device.PageFlags, v => device.PageFlags = v);
            _pageClock = AddCheck(root, "Clock", () => device.PageClock, v => device.PageClock = v);
            _pageTower = AddCheck(root, "Relative tower (ahead/behind)",
                () => device.PageTower, v => device.PageTower = v);
            _pageLogic = AddCheck(root, "Race logic (current lap, sectors, delta)",
                () => device.PageLogic, v => device.PageLogic = v);

            root.Children.Add(new TextBlock
            {
                Text = "The plugin sends this list to the display. Disabled pages are skipped by the touch cycle; " +
                       "with no page enabled the display shows NO PAGE. Flags still keep priority over the other pages.",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 620,
                FontSize = 11,
                Opacity = 0.75,
                Margin = new Thickness(0, 2, 0, 4)
            });

            _showBestLap = AddCheck(root, "Race tower: show best lap instead of last lap",
                () => device.ShowBestLap, v => device.ShowBestLap = v);

            AddSection(root, "Backlight on this display");
            _applyBrightness = AddCheck(root, "Set display brightness from SimHub",
                () => device.ApplyBrightness, v => device.ApplyBrightness = v);
            _dimWhenNoGame = AddCheck(root, "Turn backlight off (0%) while no game is running",
                () => device.DimWhenNoGame, v => device.DimWhenNoGame = v);
            _brightness = AddSlider(root, "Brightness while a game is running", 5, 100, 1,
                () => device.Brightness, v => device.Brightness = (int)Math.Round(v), "0");

            root.Children.Add(new TextBlock
            {
                Text = "With the no-game option enabled the backlight is set to 0% as soon as SimHub reports " +
                       "no running game, and this slider value is restored when a game starts. Useful when the " +
                       "display should disappear while you are only using Windows.",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 620,
                FontSize = 11,
                Opacity = 0.75,
                Margin = new Thickness(0, 2, 0, 4)
            });

            Button push = AddButton(root, "Push brightness now");
            push.Click += (s, e) =>
            {
                Apply();
                _plugin.ApplyBrightnessNow();
                RefreshStatus();
            };

            AddSection(root, "WiFi on this display");
            _wifiSsid = AddText(root, "Network name (SSID)", () => device.WifiSsid,
                v => device.WifiSsid = v ?? string.Empty, 300);
            _wifiPassword = AddPassword(root, "Password", 300);

            Button saveWifi = AddButton(root, "Save WiFi on this display and connect");
            saveWifi.Click += (s, e) =>
            {
                Apply();
                string error = _plugin.SaveWifiToDisplay(device, _wifiSsid.Text, _wifiPassword.Password);
                if (_wifiStatus != null)
                {
                    _wifiStatus.Text = string.IsNullOrEmpty(error)
                        ? "Credentials written to " + device.Name + ". It should connect within a few seconds."
                        : error;
                    _wifiStatus.Foreground = new SolidColorBrush(string.IsNullOrEmpty(error)
                        ? Color.FromRgb(0x33, 0xC4, 0x6E)
                        : Color.FromRgb(0xE0, 0x4B, 0x3C));
                }
            };

            _wifiStatus = new TextBlock
            {
                Text = "Saved in this display's NVS: survives OTA updates and reboots. A full USB/factory flash " +
                       "erases it again. The password is never stored on the PC.",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 620,
                FontSize = 11,
                Opacity = 0.75,
                Margin = new Thickness(0, 2, 0, 4)
            };
            root.Children.Add(_wifiStatus);
        }

        private void OnDeviceSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_building || _updatingSelection) return;

            int index = _deviceCombo != null ? _deviceCombo.SelectedIndex : -1;
            if (index < 0) return;
            if (index == _plugin.SelectedDeviceIndex) return;

            // Commit the controls of the display that was selected before
            // rebuilding them for the new display.
            Apply();
            _plugin.SelectedDeviceIndex = index;
            Build();
        }

        private void UpdateComboName()
        {
            if (_deviceCombo == null) return;
            int index = _deviceCombo.SelectedIndex;
            if (index < 0 || index >= _deviceCombo.Items.Count) return;

            _updatingSelection = true;
            try
            {
                _deviceCombo.Items[index] = _device != null ? _device.Name : string.Empty;
                _deviceCombo.SelectedIndex = index;
            }
            finally
            {
                _updatingSelection = false;
            }
        }

        private static int ParseInt(string text, int fallback)
        {
            int value;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private void Tick()
        {
            // Coalesce edits: the timer applies changes at most twice a second,
            // so dragging the brightness slider does not flood the display.
            if (_dirty && (DateTime.UtcNow - _lastApplyUtc).TotalMilliseconds > 350)
                Apply();

            RefreshStatus();
        }

        private void Apply()
        {
            if (_plugin == null || Settings == null || _building)
            {
                _dirty = false;
                return;
            }

            Settings.Enabled = _enabled != null && _enabled.IsChecked == true;
            if (_heartbeat != null)
                Settings.HeartbeatMs = ParseInt(_heartbeat.Text, Settings.HeartbeatMs);
            if (_minInterval != null)
                Settings.MinSendIntervalMs = ParseInt(_minInterval.Text, Settings.MinSendIntervalMs);
            Settings.LogRequests = _logRequests != null && _logRequests.IsChecked == true;

            CydFlagsDeviceSettings device = _device;
            if (device != null)
            {
                device.Enabled = _deviceEnabled != null && _deviceEnabled.IsChecked == true;
                if (_deviceName != null) device.Name = (_deviceName.Text ?? string.Empty).Trim();
                if (_host != null) device.Host = (_host.Text ?? string.Empty).Trim();

                int value;
                if (_port != null
                    && int.TryParse((_port.Text ?? string.Empty).Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out value))
                {
                    device.Port = value;
                }

                device.ShowBestLap = _showBestLap != null && _showBestLap.IsChecked == true;

                device.PageFlags = _pageFlags != null && _pageFlags.IsChecked == true;
                device.PageClock = _pageClock != null && _pageClock.IsChecked == true;
                device.PageTower = _pageTower != null && _pageTower.IsChecked == true;
                device.PageLogic = _pageLogic != null && _pageLogic.IsChecked == true;

                if (_wifiSsid != null)
                    device.WifiSsid = (_wifiSsid.Text ?? string.Empty).Trim();

                device.ApplyBrightness = _applyBrightness != null && _applyBrightness.IsChecked == true;
                device.DimWhenNoGame = _dimWhenNoGame != null && _dimWhenNoGame.IsChecked == true;
                if (_brightness != null) device.Brightness = (int)Math.Round(_brightness.Value);
            }

            _plugin.ApplySettings();
            _dirty = false;
            _lastApplyUtc = DateTime.UtcNow;
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        private void ShowTestFlag()
        {
            string option = _testFlag.SelectedItem as string;
            FlagKind kind = FlagModel.ParseName(option);
            _plugin.ShowTestFlag(kind, 10);
            if (_testStatus != null)
                _testStatus.Text = "Showing " + FlagModel.ToOption(kind) + " on all enabled displays for 10 seconds.";
        }

        private void RefreshStatus()
        {
            if (_linkLine == null || _plugin == null) return;

            try
            {
                bool linked = _plugin.Linked;
                int index = _plugin.SelectedDeviceIndex;
                int count = _plugin.DeviceCount;
                string name = _device != null ? _device.Name : "-";

                _linkLine.Text = (linked ? "LINK online" : "LINK offline")
                    + "   -   " + name + " (" + (index + 1) + "/" + count + ")";
                _linkLine.Foreground = new SolidColorBrush(linked
                    ? Color.FromRgb(0x33, 0xC4, 0x6E)
                    : Color.FromRgb(0xE0, 0x4B, 0x3C));

                DateTime last = _plugin.LastReplyUtc;
                string lastText = last == default(DateTime)
                    ? "never"
                    : last.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

                string url = _plugin.DeviceUrl;
                if (string.IsNullOrEmpty(url)) url = _plugin.DescribeTarget();
                string game = string.IsNullOrEmpty(_plugin.CurrentGame) ? "-" : _plugin.CurrentGame;
                string error = string.IsNullOrEmpty(_plugin.LastError) ? "-" : _plugin.LastError;

                _detailLine.Text = string.Format(
                    CultureInfo.InvariantCulture,
                    "display   {0} ({1}/{2})\nurl       {3}\nlast      {4}    requests {5}\n" +
                    "flag      {6,-10} game {7}\nerror     {8}",
                    name,
                    index + 1,
                    count,
                    url,
                    lastText,
                    _plugin.RequestsSent,
                    FlagModel.ToOption(_plugin.CurrentFlag),
                    game,
                    error);
            }
            catch (Exception)
            {
                // The settings view can outlive the plugin during shutdown.
            }
        }

        // ------------------------------------------------------------------
        // Small UI builder helpers.
        // ------------------------------------------------------------------
        private void AddSection(Panel root, string title)
        {
            root.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 14, 0, 4)
            });
            root.Children.Add(new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(0, 0, 0, 6)
            });
        }

        private CheckBox AddCheck(Panel root, string label, Func<bool> get, Action<bool> set)
        {
            var box = new CheckBox
            {
                Content = label,
                IsChecked = get(),
                Margin = new Thickness(0, 3, 0, 3)
            };
            box.Checked += (s, e) => { set(true); MarkDirty(); };
            box.Unchecked += (s, e) => { set(false); MarkDirty(); };
            root.Children.Add(box);
            return box;
        }

        private TextBox AddText(Panel root, string label, Func<string> get, Action<string> set, double boxWidth)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            panel.Children.Add(new TextBlock
            {
                Text = label,
                Width = 260,
                VerticalAlignment = VerticalAlignment.Center
            });

            var box = new TextBox
            {
                Width = boxWidth,
                Text = get(),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            // Apply on focus loss / Enter rather than on every keystroke: a
            // half-typed hostname would otherwise trigger useless requests.
            box.LostFocus += (s, e) => { set(box.Text); MarkDirty(); };
            box.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    set(box.Text);
                    MarkDirty();
                    box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            };

            panel.Children.Add(box);
            root.Children.Add(panel);
            return box;
        }

        private PasswordBox AddPassword(Panel root, string label, double boxWidth)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            panel.Children.Add(new TextBlock
            {
                Text = label,
                Width = 260,
                VerticalAlignment = VerticalAlignment.Center
            });
            var box = new PasswordBox
            {
                Width = boxWidth,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(box);
            root.Children.Add(panel);
            return box;
        }

        private Slider AddSlider(Panel root, string label, double min, double max, double step,
            Func<double> get, Action<double> set, string format)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

            panel.Children.Add(new TextBlock
            {
                Text = label,
                Width = 260,
                VerticalAlignment = VerticalAlignment.Center
            });

            var box = new TextBox
            {
                Width = 64,
                Text = get().ToString(format, CultureInfo.InvariantCulture),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            var slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                SmallChange = step,
                LargeChange = step * 10,
                Value = Math.Max(min, Math.Min(max, get())),
                Width = 300,
                VerticalAlignment = VerticalAlignment.Center,
                IsSnapToTickEnabled = false
            };

            bool updating = false;
            slider.ValueChanged += (s, e) =>
            {
                if (updating) return;
                updating = true;
                box.Text = e.NewValue.ToString(format, CultureInfo.InvariantCulture);
                set(e.NewValue);
                MarkDirty();
                updating = false;
            };
            box.LostFocus += (s, e) =>
            {
                if (updating) return;
                double v;
                if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                {
                    v = Math.Max(min, Math.Min(max, v));
                    updating = true;
                    slider.Value = v;
                    box.Text = v.ToString(format, CultureInfo.InvariantCulture);
                    set(v);
                    MarkDirty();
                    updating = false;
                }
                else
                {
                    box.Text = slider.Value.ToString(format, CultureInfo.InvariantCulture);
                }
            };

            panel.Children.Add(box);
            panel.Children.Add(slider);
            root.Children.Add(panel);
            return slider;
        }

        private Button AddButton(Panel root, string text)
        {
            Button button = NewButton(text, 0);
            button.HorizontalAlignment = HorizontalAlignment.Left;
            button.Margin = new Thickness(0, 8, 0, 0);
            root.Children.Add(button);
            return button;
        }

        private static Button NewButton(string text, double leftMargin)
        {
            return new Button
            {
                Content = text,
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(leftMargin, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
        }
    }
}
