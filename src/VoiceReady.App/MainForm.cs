using VoiceReady.Core.Audio;
using VoiceReady.Core.Commands;
using VoiceReady.Core.Configuration;
using VoiceReady.Core.Detection;
using VoiceReady.Core.Input;
using VoiceReady.Core.Memory;
using VoiceReady.Core.Transcription;

namespace VoiceReady.App;

public sealed class MainForm : Form
{
    private readonly ComboBox _microphoneCombo = new();
    private readonly CheckBox _automaticThresholdCheck = new();
    private readonly TrackBar _startThresholdSlider = new();
    private readonly Label _startThresholdValueLabel = new();
    private readonly Label _endThresholdValueLabel = new();
    private readonly NumericUpDown _minimumSpeechInput = new();
    private readonly NumericUpDown _trailingSilenceInput = new();
    private readonly Button _startStopButton = new();
    private readonly Button _refreshDevicesButton = new();
    private readonly CheckBox _debugCheck = new();
    private readonly RichTextBox _debugLog = new();
    private readonly ProgressBar _levelMeter = new();
    private readonly Label _statusLabel = new();
    private readonly Label _menuStateLabel = new();
    private readonly Label _teamStateLabel = new();
    private readonly Label _speechLabel = new();
    private readonly Label _voiceResultLabel = new();
    private readonly System.Windows.Forms.Timer _stateTimer = new();

    private RuntimeContext? _runtime;
    private CancellationTokenSource? _shutdown;
    private VoiceReadySettings _baseSettings = new();
    private MemoryMap? _memoryMap;
    private CommandMenuMap? _commandMenuMap;
    private string _repoRoot = string.Empty;
    private string _configDir = string.Empty;
    private int? _lastMenuValue;
    private int? _lastTeamValue;
    private bool _hasLoggedRoots;

    public MainForm()
    {
        Text = "VoiceReady";
        MinimumSize = new Size(920, 680);
        Size = new Size(1040, 740);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 247, 250);

        BuildLayout();
        Load += OnLoad;
        FormClosing += OnFormClosing;

        _stateTimer.Interval = 50;
        _stateTimer.Tick += (_, _) => RefreshRuntimeState();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18),
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
        Controls.Add(root);

        var title = new Label
        {
            Text = "VoiceReady",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 22, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 31, 42),
            Margin = new Padding(0, 0, 0, 4)
        };
        root.Controls.Add(title, 0, 0);

        var subtitle = new Label
        {
            Text = "Ready or Not voice command assistant",
            AutoSize = true,
            ForeColor = Color.FromArgb(81, 91, 107),
            Margin = new Padding(1, 0, 0, 18)
        };
        root.Controls.Add(subtitle, 0, 1);

        var mainGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        root.Controls.Add(mainGrid, 0, 2);

        mainGrid.Controls.Add(BuildSetupPanel(), 0, 0);
        mainGrid.Controls.Add(BuildStatusPanel(), 1, 0);

        var debugPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 14, 0, 0)
        };
        debugPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        debugPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(debugPanel, 0, 3);

        var debugHeader = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 6)
        };
        _debugCheck.Text = "Show debug details";
        _debugCheck.AutoSize = true;
        _debugCheck.CheckedChanged += (_, _) => _debugLog.Visible = _debugCheck.Checked;
        debugHeader.Controls.Add(_debugCheck);
        debugPanel.Controls.Add(debugHeader, 0, 0);

        _debugLog.Dock = DockStyle.Fill;
        _debugLog.ReadOnly = true;
        _debugLog.BorderStyle = BorderStyle.FixedSingle;
        _debugLog.BackColor = Color.FromArgb(20, 24, 33);
        _debugLog.ForeColor = Color.FromArgb(225, 231, 240);
        _debugLog.Font = new Font("Consolas", 9);
        _debugLog.Visible = false;
        debugPanel.Controls.Add(_debugLog, 0, 1);
    }

    private Control BuildSetupPanel()
    {
        var panel = CreatePanel("Setup");
        var layout = (TableLayoutPanel)panel.Controls[0];

        AddLabel(layout, "Microphone");
        var deviceRow = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true };
        deviceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        deviceRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _microphoneCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _microphoneCombo.Dock = DockStyle.Top;
        _refreshDevicesButton.Text = "Refresh";
        _refreshDevicesButton.AutoSize = true;
        _refreshDevicesButton.Click += (_, _) => LoadMicrophones();
        deviceRow.Controls.Add(_microphoneCombo, 0, 0);
        deviceRow.Controls.Add(_refreshDevicesButton, 1, 0);
        layout.Controls.Add(deviceRow);

        _automaticThresholdCheck.Text = "Automatic speech threshold";
        _automaticThresholdCheck.AutoSize = true;
        _automaticThresholdCheck.Checked = true;
        _automaticThresholdCheck.Margin = new Padding(0, 14, 0, 4);
        _automaticThresholdCheck.CheckedChanged += (_, _) => UpdateThresholdControls();
        layout.Controls.Add(_automaticThresholdCheck);

        AddLabel(layout, "Voice trigger threshold");
        _startThresholdSlider.Minimum = -70;
        _startThresholdSlider.Maximum = -15;
        _startThresholdSlider.Value = -35;
        _startThresholdSlider.TickFrequency = 5;
        _startThresholdSlider.Dock = DockStyle.Top;
        _startThresholdSlider.ValueChanged += (_, _) => UpdateThresholdLabels();
        layout.Controls.Add(_startThresholdSlider);
        _startThresholdValueLabel.AutoSize = true;
        _startThresholdValueLabel.ForeColor = Color.FromArgb(81, 91, 107);
        layout.Controls.Add(_startThresholdValueLabel);

        _endThresholdValueLabel.AutoSize = true;
        _endThresholdValueLabel.ForeColor = Color.FromArgb(81, 91, 107);
        layout.Controls.Add(_endThresholdValueLabel);

        var timingGrid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true };
        timingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        timingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        AddNumericSetting(timingGrid, "Minimum speech ms", _minimumSpeechInput, 100, 1500, 350);
        AddNumericSetting(timingGrid, "Silence to finish ms", _trailingSilenceInput, 150, 2000, 550);
        layout.Controls.Add(timingGrid);

        _startStopButton.Text = "Start VoiceReady";
        _startStopButton.Height = 44;
        _startStopButton.Dock = DockStyle.Top;
        _startStopButton.FlatStyle = FlatStyle.Flat;
        _startStopButton.BackColor = Color.FromArgb(32, 103, 214);
        _startStopButton.ForeColor = Color.White;
        _startStopButton.FlatAppearance.BorderSize = 0;
        _startStopButton.Margin = new Padding(0, 18, 0, 0);
        _startStopButton.Click += async (_, _) => await ToggleRuntimeAsync();
        layout.Controls.Add(_startStopButton);

        UpdateThresholdLabels();
        UpdateThresholdControls();
        return panel;
    }

    private Control BuildStatusPanel()
    {
        var panel = CreatePanel("Live status");
        var layout = (TableLayoutPanel)panel.Controls[0];

        _statusLabel.Text = "Stopped";
        _statusLabel.Font = new Font(Font.FontFamily, 13, FontStyle.Bold);
        _statusLabel.ForeColor = Color.FromArgb(148, 49, 49);
        _statusLabel.AutoSize = true;
        layout.Controls.Add(_statusLabel);

        _levelMeter.Minimum = 0;
        _levelMeter.Maximum = 100;
        _levelMeter.Height = 18;
        _levelMeter.Dock = DockStyle.Top;
        _levelMeter.Margin = new Padding(0, 14, 0, 10);
        layout.Controls.Add(_levelMeter);

        _menuStateLabel.Text = "Menu: not connected";
        _teamStateLabel.Text = "Team: not connected";
        _speechLabel.Text = "Heard: none";
        _voiceResultLabel.Text = "Command: waiting";

        foreach (var label in new[] { _menuStateLabel, _teamStateLabel, _speechLabel, _voiceResultLabel })
        {
            label.AutoSize = false;
            label.Height = 42;
            label.Dock = DockStyle.Top;
            label.ForeColor = Color.FromArgb(36, 44, 57);
            label.Padding = new Padding(0, 8, 0, 0);
            layout.Controls.Add(label);
        }

        return panel;
    }

    private static Panel CreatePanel(string heading)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(18),
            Margin = new Padding(0, 0, 14, 0)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            AutoScroll = true
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = heading,
            AutoSize = true,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 31, 42),
            Margin = new Padding(0, 0, 0, 14)
        };
        layout.Controls.Add(title);
        panel.Controls.Add(layout);
        return panel;
    }

    private static void AddLabel(TableLayoutPanel layout, string text)
    {
        layout.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.FromArgb(81, 91, 107),
            Margin = new Padding(0, 10, 0, 4)
        });
    }

    private static void AddNumericSetting(TableLayoutPanel layout, string label, NumericUpDown input, int min, int max, int value)
    {
        var group = new TableLayoutPanel { Dock = DockStyle.Top, RowCount = 2, AutoSize = true, Margin = new Padding(0, 12, 8, 0) };
        group.Controls.Add(new Label { Text = label, AutoSize = true, ForeColor = Color.FromArgb(81, 91, 107) });
        input.Minimum = min;
        input.Maximum = max;
        input.Value = value;
        input.Increment = 25;
        input.Dock = DockStyle.Top;
        group.Controls.Add(input);
        layout.Controls.Add(group);
    }

    private void OnLoad(object? sender, EventArgs eventArgs)
    {
        _repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        _configDir = Path.Combine(_repoRoot, "config");
        LoadConfiguration();
        LoadMicrophones();
    }

    private void LoadConfiguration()
    {
        var memoryMapPath = Path.Combine(_configDir, "memory_map.json");
        var commandMenuMapPath = Path.Combine(_configDir, "command_menus.json");
        var voiceSettingsPath = Path.Combine(_configDir, "voice_ready.json");

        _memoryMap = MemoryMapLoader.Load(memoryMapPath);
        _commandMenuMap = File.Exists(commandMenuMapPath) ? CommandMenuMapLoader.Load(commandMenuMapPath) : null;
        _baseSettings = File.Exists(voiceSettingsPath) ? VoiceReadySettingsLoader.Load(voiceSettingsPath) : new VoiceReadySettings();

        _startThresholdSlider.Value = Clamp((int)Math.Round(_baseSettings.Audio.SpeechStartDb), _startThresholdSlider.Minimum, _startThresholdSlider.Maximum);
        _minimumSpeechInput.Value = Clamp(_baseSettings.Audio.MinimumSpeechMilliseconds, (int)_minimumSpeechInput.Minimum, (int)_minimumSpeechInput.Maximum);
        _trailingSilenceInput.Value = Clamp(_baseSettings.Audio.TrailingSilenceMilliseconds, (int)_trailingSilenceInput.Minimum, (int)_trailingSilenceInput.Maximum);
        UpdateThresholdLabels();
    }

    private void LoadMicrophones()
    {
        _microphoneCombo.Items.Clear();
        var devices = WaveInAudioSource.GetInputDevices();
        foreach (var device in devices)
        {
            _microphoneCombo.Items.Add(new DeviceItem(device));
        }

        if (_microphoneCombo.Items.Count == 0)
        {
            _microphoneCombo.Items.Add(new DeviceItem(new AudioInputDevice(-1, "Default recording device")));
        }

        var targetDevice = _baseSettings.Audio.DeviceNumber;
        for (var i = 0; i < _microphoneCombo.Items.Count; i++)
        {
            if (_microphoneCombo.Items[i] is DeviceItem item && item.Device.DeviceNumber == targetDevice)
            {
                _microphoneCombo.SelectedIndex = i;
                return;
            }
        }

        _microphoneCombo.SelectedIndex = 0;
    }

    private async Task ToggleRuntimeAsync()
    {
        if (_runtime is null)
        {
            await StartRuntimeAsync();
        }
        else
        {
            await StopRuntimeAsync();
        }
    }

    private async Task StartRuntimeAsync()
    {
        if (_memoryMap is null)
        {
            return;
        }

        SetControlsRunning(true);
        _statusLabel.Text = "Starting";
        _statusLabel.ForeColor = Color.FromArgb(139, 95, 25);
        _debugLog.Clear();
        _lastMenuValue = null;
        _lastTeamValue = null;
        _hasLoggedRoots = false;

        try
        {
            _shutdown = new CancellationTokenSource();
            var settings = BuildRuntimeSettings();
            var processReader = ProcessMemoryReader.AttachByProcessName(_memoryMap.ProcessNames);
            var menuReader = new MenuStateReader(processReader, _memoryMap.MenuState.PointerPaths);
            var teamSelectionReader = new MenuStateReader(processReader, _memoryMap.TeamSelection.PointerPaths);
            var keyboardInput = new KeyboardInput();
            var parser = new VoiceCommandParser();
            var executor = new CommandPlanExecutor(
                menuReader,
                _memoryMap.MenuState.KnownStates,
                teamSelectionReader,
                _memoryMap.TeamSelection.KnownSelections,
                keyboardInput,
                settings.Input);

            _runtime = new RuntimeContext(processReader, menuReader, teamSelectionReader, keyboardInput, parser, executor, settings);
            LogDebug($"Attached to {processReader.ProcessName} ({processReader.ProcessId}).");
            LogDebug($"Reading {_memoryMap.MenuState.PointerPaths.Count} menu-state pointer paths.");
            LogDebug($"Reading {_memoryMap.TeamSelection.PointerPaths.Count} team-selection pointer paths.");

            _runtime.VoiceTask = Task.Run(() => RunVoiceLoopAsync(_runtime, _shutdown.Token));
            _stateTimer.Start();
            _statusLabel.Text = "Running";
            _statusLabel.ForeColor = Color.FromArgb(28, 115, 73);
            _startStopButton.Text = "Stop VoiceReady";
        }
        catch (Exception ex)
        {
            LogDebug($"Startup error: {ex.Message}");
            _statusLabel.Text = "Could not start";
            _statusLabel.ForeColor = Color.FromArgb(148, 49, 49);
            MessageBox.Show(this, ex.Message, "VoiceReady startup failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            await StopRuntimeAsync();
        }
    }

    private async Task StopRuntimeAsync()
    {
        _stateTimer.Stop();
        _shutdown?.Cancel();

        var runtime = _runtime;
        _runtime = null;
        if (runtime?.VoiceTask is not null)
        {
            try
            {
                await runtime.VoiceTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // The background loop is allowed to finish during disposal.
            }
        }

        runtime?.Dispose();
        _shutdown?.Dispose();
        _shutdown = null;
        _levelMeter.Value = 0;
        _statusLabel.Text = "Stopped";
        _statusLabel.ForeColor = Color.FromArgb(148, 49, 49);
        _startStopButton.Text = "Start VoiceReady";
        SetControlsRunning(false);
    }

    private async Task RunVoiceLoopAsync(RuntimeContext runtime, CancellationToken cancellationToken)
    {
        try
        {
            using var audioSource = new WaveInAudioSource(runtime.Settings.Audio);
            audioSource.Start();
            LogDebug("Microphone capture started.");

            var activeSettings = runtime.Settings;
            if (_automaticThresholdCheck.Checked)
            {
                activeSettings = await CalibrateSettingsAsync(audioSource, activeSettings, cancellationToken);
            }

            using var transcriber = new VoskTranscriber(activeSettings.Vosk, _repoRoot, activeSettings.Audio.SampleRate);
            var segmenter = new SpeechSegmenter(activeSettings.Audio);
            LogDebug("Local Vosk recognition started.");

            while (!cancellationToken.IsCancellationRequested)
            {
                while (audioSource.TryRead(out var frame) && frame is not null)
                {
                    var segment = segmenter.Process(frame);
                    BeginInvoke(() => UpdateLevel(segmenter.CurrentDecibels, segmenter.IsSpeaking));

                    if (segment is null)
                    {
                        continue;
                    }

                    try
                    {
                        var result = transcriber.Transcribe(segment);
                        if (result is null)
                        {
                            continue;
                        }

                        BeginInvoke(() => _speechLabel.Text = $"Heard: {result.Text}");
                        LogDebug($"{DateTimeOffset.Now:HH:mm:ss.fff} speech=\"{result.Text}\"");

                        var plan = runtime.Parser.Parse(result.Text);
                        if (plan is null)
                        {
                            BeginInvoke(() => _voiceResultLabel.Text = "Command: ignored - no match");
                            LogDebug($"{DateTimeOffset.Now:HH:mm:ss.fff} voice result=ignored No command matched.");
                            continue;
                        }

                        var executed = runtime.Executor.TryExecute(plan, out var message);
                        BeginInvoke(() => _voiceResultLabel.Text = $"Command: {(executed ? "sent" : "blocked")} - {message}");
                        LogDebug($"{DateTimeOffset.Now:HH:mm:ss.fff} voice result={(executed ? "sent" : "blocked")} {message}");
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        BeginInvoke(() => _voiceResultLabel.Text = $"Command: error - {ex.Message}");
                        LogDebug($"{DateTimeOffset.Now:HH:mm:ss.fff} voice result=error {ex.Message}");
                    }
                }

                await Task.Delay(10, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            BeginInvoke(() =>
            {
                _statusLabel.Text = "Voice error";
                _statusLabel.ForeColor = Color.FromArgb(148, 49, 49);
                _voiceResultLabel.Text = $"Command: error - {ex.Message}";
            });
            LogDebug($"Voice loop error: {ex.Message}");
        }
    }

    private async Task<VoiceReadySettings> CalibrateSettingsAsync(WaveInAudioSource audioSource, VoiceReadySettings settings, CancellationToken cancellationToken)
    {
        BeginInvoke(() => _statusLabel.Text = "Calibrating microphone");
        var samples = new List<double>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);

        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            while (audioSource.TryRead(out var frame) && frame is not null)
            {
                var decibels = SpeechSegmenter.CalculateDecibels(frame.Data);
                samples.Add(decibels);
                BeginInvoke(() => UpdateLevel(decibels, false));
            }

            await Task.Delay(10, cancellationToken);
        }

        if (samples.Count == 0)
        {
            LogDebug("Automatic threshold calibration had no samples; using configured thresholds.");
            return settings;
        }

        var ambient = samples.OrderBy(value => value).ElementAt(samples.Count / 2);
        var start = Clamp(ambient + 12, -55, -24);
        var end = Clamp(start - 7, -65, start - 3);
        LogDebug($"Automatic threshold calibrated ambient={ambient:F1} dB start={start:F1} dB end={end:F1} dB.");

        BeginInvoke(() =>
        {
            _statusLabel.Text = "Running";
            _startThresholdValueLabel.Text = $"Voice trigger: {start:F0} dB automatic";
            _endThresholdValueLabel.Text = $"Speech ends below: {end:F0} dB automatic";
        });

        return ReplaceAudioSettings(settings, start, end);
    }

    private void RefreshRuntimeState()
    {
        if (_runtime is null || _memoryMap is null)
        {
            return;
        }

        if (!_hasLoggedRoots)
        {
            foreach (var root in _runtime.ProcessReader.RootResolutions.OrderBy(root => root.ConfiguredOffset))
            {
                var source = root.UsedSignature ? "signature" : "fallback";
                var moved = root.ConfiguredOffset == root.ResolvedOffset ? string.Empty : $" moved-from=+0x{root.ConfiguredOffset:X}";
                var reason = string.IsNullOrWhiteSpace(root.FallbackReason) ? string.Empty : $" reason={root.FallbackReason}";
                LogDebug($"Pointer root {root.ModuleName}+0x{root.ResolvedOffset:X} source={source}{moved}{reason}");
            }

            _hasLoggedRoots = true;
        }

        var snapshot = _runtime.MenuReader.Read();
        var teamSnapshot = _runtime.TeamSelectionReader.Read();
        var knownStates = _memoryMap.MenuState.KnownStates.ToDictionary(
            state => state.Value,
            state => state.Aliases.Count == 0 ? state.Name : $"{state.Name}/{string.Join("/", state.Aliases)}");
        var commandStates = _commandMenuMap?.States
            .GroupBy(state => state.MemoryValue)
            .ToDictionary(group => group.Key, group => group.ToArray())
            ?? [];

        var stateName = snapshot.VotedValue.HasValue && knownStates.TryGetValue(snapshot.VotedValue.Value, out var knownState)
            ? knownState
            : "Unmapped";
        _menuStateLabel.Text = snapshot.VotedValue.HasValue
            ? $"Menu: {snapshot.VotedValue.Value} ({stateName}), confidence {snapshot.Confidence:P0}"
            : $"Menu: no consensus, reads {snapshot.SuccessfulReads}, failed {snapshot.FailedReads}";

        if (snapshot.VotedValue != _lastMenuValue)
        {
            var valueText = snapshot.VotedValue.HasValue ? $"{snapshot.VotedValue.Value} ({stateName})" : "no consensus";
            var commandText = snapshot.VotedValue.HasValue && commandStates.TryGetValue(snapshot.VotedValue.Value, out var matchingCommandStates)
                ? $", states={string.Join("/", matchingCommandStates.Select(state => state.Name))}, commands={string.Join("/", matchingCommandStates.Select(state => state.Commands.Count))}"
                : string.Empty;
            LogDebug($"{DateTimeOffset.Now:HH:mm:ss.fff} value={valueText}, confidence={snapshot.Confidence:P0}, reads={snapshot.SuccessfulReads}, failed={snapshot.FailedReads}{commandText}");
            LogFailedReads(snapshot);
            _lastMenuValue = snapshot.VotedValue;
        }

        var teamName = _memoryMap.TeamSelection.KnownSelections
            .FirstOrDefault(selection => selection.Value == teamSnapshot.VotedValue)?.Name
            ?? "Unmapped";
        _teamStateLabel.Text = teamSnapshot.VotedValue.HasValue
            ? $"Team: {teamSnapshot.VotedValue.Value} ({teamName}), confidence {teamSnapshot.Confidence:P0}"
            : $"Team: no consensus, reads {teamSnapshot.SuccessfulReads}, failed {teamSnapshot.FailedReads}";

        if (teamSnapshot.VotedValue != _lastTeamValue)
        {
            LogDebug($"{DateTimeOffset.Now:HH:mm:ss.fff} team={teamSnapshot.VotedValue?.ToString() ?? "no consensus"} ({teamName}), confidence={teamSnapshot.Confidence:P0}, reads={teamSnapshot.SuccessfulReads}, failed={teamSnapshot.FailedReads}");
            _lastTeamValue = teamSnapshot.VotedValue;
        }
    }

    private void LogFailedReads(MenuStateSnapshot snapshot)
    {
        if (snapshot.SuccessfulReads != 0)
        {
            return;
        }

        foreach (var failedRead in snapshot.Reads.Where(read => !read.Success).Take(5))
        {
            LogDebug($"  {failedRead.Pointer.Name}: {failedRead.Error}");
        }
    }

    private VoiceReadySettings BuildRuntimeSettings()
    {
        var device = _microphoneCombo.SelectedItem is DeviceItem item ? item.Device.DeviceNumber : _baseSettings.Audio.DeviceNumber;
        var start = _startThresholdSlider.Value;
        var end = start - 7;
        var settings = ReplaceAudioSettings(_baseSettings, start, end, device);
        return settings;
    }

    private VoiceReadySettings ReplaceAudioSettings(VoiceReadySettings settings, double startDb, double endDb, int? deviceNumber = null)
    {
        var audio = settings.Audio;
        return new VoiceReadySettings
        {
            Audio = new AudioSettings
            {
                DeviceNumber = deviceNumber ?? audio.DeviceNumber,
                SampleRate = audio.SampleRate,
                Channels = audio.Channels,
                FrameMilliseconds = audio.FrameMilliseconds,
                SpeechStartDb = startDb,
                SpeechEndDb = endDb,
                MinimumSpeechMilliseconds = (int)_minimumSpeechInput.Value,
                TrailingSilenceMilliseconds = (int)_trailingSilenceInput.Value,
                MaximumSegmentMilliseconds = audio.MaximumSegmentMilliseconds
            },
            Vosk = settings.Vosk,
            Input = settings.Input
        };
    }

    private void UpdateLevel(double decibels, bool speaking)
    {
        var value = Clamp((int)Math.Round((decibels + 70) / 55 * 100), 0, 100);
        _levelMeter.Value = value;
        _statusLabel.Text = _runtime is null ? "Stopped" : speaking ? "Listening" : "Running";
    }

    private void UpdateThresholdControls()
    {
        _startThresholdSlider.Enabled = !_automaticThresholdCheck.Checked;
        UpdateThresholdLabels();
    }

    private void UpdateThresholdLabels()
    {
        var start = _startThresholdSlider.Value;
        var end = start - 7;
        var suffix = _automaticThresholdCheck.Checked ? " automatic baseline before start" : string.Empty;
        _startThresholdValueLabel.Text = $"Voice trigger: {start} dB{suffix}";
        _endThresholdValueLabel.Text = $"Speech ends below: {end} dB";
    }

    private void SetControlsRunning(bool running)
    {
        _microphoneCombo.Enabled = !running;
        _refreshDevicesButton.Enabled = !running;
        _automaticThresholdCheck.Enabled = !running;
        _startThresholdSlider.Enabled = !running && !_automaticThresholdCheck.Checked;
        _minimumSpeechInput.Enabled = !running;
        _trailingSilenceInput.Enabled = !running;
        _startStopButton.Enabled = true;
    }

    private void LogDebug(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => LogDebug(message));
            return;
        }

        _debugLog.AppendText(message + Environment.NewLine);
        _debugLog.SelectionStart = _debugLog.TextLength;
        _debugLog.ScrollToCaret();
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (_runtime is null)
        {
            return;
        }

        eventArgs.Cancel = true;
        await StopRuntimeAsync();
        Close();
    }

    private static string FindRepoRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VoiceReady.slnx")) || Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);

    private static double Clamp(double value, double min, double max) => Math.Min(Math.Max(value, min), max);

    private sealed class DeviceItem(AudioInputDevice device)
    {
        public AudioInputDevice Device { get; } = device;

        public override string ToString() => $"{Device.Name} ({Device.DeviceNumber})";
    }

    private sealed class RuntimeContext(
        ProcessMemoryReader processReader,
        MenuStateReader menuReader,
        MenuStateReader teamSelectionReader,
        KeyboardInput keyboardInput,
        VoiceCommandParser parser,
        CommandPlanExecutor executor,
        VoiceReadySettings settings) : IDisposable
    {
        public ProcessMemoryReader ProcessReader { get; } = processReader;

        public MenuStateReader MenuReader { get; } = menuReader;

        public MenuStateReader TeamSelectionReader { get; } = teamSelectionReader;

        public KeyboardInput KeyboardInput { get; } = keyboardInput;

        public VoiceCommandParser Parser { get; } = parser;

        public CommandPlanExecutor Executor { get; } = executor;

        public VoiceReadySettings Settings { get; } = settings;

        public Task? VoiceTask { get; set; }

        public void Dispose()
        {
            ProcessReader.Dispose();
        }
    }
}
