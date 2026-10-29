using VoiceReady.Core.Audio;
using VoiceReady.Core.Commands;
using VoiceReady.Core.Configuration;
using VoiceReady.Core.Detection;
using VoiceReady.Core.Input;
using VoiceReady.Core.Memory;
using VoiceReady.Core.Transcription;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VoiceReady.App;

public sealed class MainForm : Form
{
    private readonly ComboBox _microphoneCombo = new();
    private readonly CheckBox _automaticThresholdCheck = new();
    private readonly DarkSlider _startThresholdSlider = new();
    private readonly Label _startThresholdValueLabel = new();
    private readonly Label _endThresholdValueLabel = new();
    private readonly DarkNumberInput _minimumSpeechInput = new();
    private readonly DarkNumberInput _trailingSilenceInput = new();
    private readonly Button _startStopButton = new();
    private readonly Button _refreshDevicesButton = new();
    private readonly Button _microphoneTestButton = new();
    private readonly LevelMeter _microphoneTestLevelMeter = new();
    private readonly Label _microphoneTestLabel = new();
    private readonly CheckBox _debugCheck = new();
    private readonly RichTextBox _debugLog = new();
    private readonly LevelMeter _levelMeter = new();
    private readonly Label _statusLabel = new();
    private readonly Label _menuStateLabel = new();
    private readonly Label _teamStateLabel = new();
    private readonly Label _speechLabel = new();
    private readonly Label _voiceResultLabel = new();
    private readonly Label _gameExecutableLabel = new();
    private readonly System.Windows.Forms.Timer _stateTimer = new();
    private readonly Dictionary<string, KeybindRow> _keybindRows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Button> _navButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Button _resetAllKeybindsButton = new();
    private readonly Panel _contentHost = new();

    private RuntimeContext? _runtime;
    private CancellationTokenSource? _shutdown;
    private CancellationTokenSource? _microphoneTestShutdown;
    private Task? _microphoneTestTask;
    private VoiceReadySettings _baseSettings = new();
    private MemoryMap? _memoryMap;
    private CommandMenuMap? _commandMenuMap;
    private string _repoRoot = string.Empty;
    private string _configDir = string.Empty;
    private string _voiceSettingsPath = string.Empty;
    private string _diagnosticLogPath = string.Empty;
    private string? _pendingKeybindId;
    private int? _lastMenuValue;
    private int? _lastTeamValue;
    private bool _hasLoggedRoots;
    private string _activePage = "dashboard";
    private string _lastMicrophoneTestTranscript = "none";
    private readonly bool _voiceTestMode;

    private static readonly Color AppBackground = Color.FromArgb(11, 12, 15);
    private static readonly Color SidebarBackground = Color.FromArgb(16, 18, 23);
    private static readonly Color PanelBackground = Color.FromArgb(22, 25, 31);
    private static readonly Color PanelBackgroundAlt = Color.FromArgb(31, 35, 43);
    private static readonly Color PanelHover = Color.FromArgb(40, 45, 56);
    private static readonly Color BorderColor = Color.FromArgb(46, 52, 64);
    private static readonly Color TextPrimary = Color.FromArgb(236, 238, 242);
    private static readonly Color TextSecondary = Color.FromArgb(150, 158, 172);
    private static readonly Color Accent = Color.FromArgb(86, 142, 255);
    private static readonly Color AccentHover = Color.FromArgb(112, 162, 255);
    private static readonly Color AccentMuted = Color.FromArgb(30, 40, 62);
    private static readonly Color Danger = Color.FromArgb(239, 100, 97);
    private static readonly Color Success = Color.FromArgb(87, 201, 140);
    private static readonly Color Warning = Color.FromArgb(232, 172, 85);
    private static readonly string[] RequiredVoskModelFiles =
    [
        "conf\\model.conf",
        "am\\final.mdl",
        "graph\\HCLr.fst",
        "graph\\Gr.fst",
        "graph\\phones\\word_boundary.int",
        "ivector\\final.ie"
    ];

    private static readonly string[] RequiredVoskNativeFiles =
    [
        "tools\\vendor\\vosk\\lib\\netstandard2.0\\Vosk.dll",
        "tools\\vendor\\vosk\\native\\win-x64\\libvosk.dll",
        "tools\\vendor\\vosk\\native\\win-x64\\libstdc++-6.dll",
        "tools\\vendor\\vosk\\native\\win-x64\\libgcc_s_seh-1.dll",
        "tools\\vendor\\vosk\\native\\win-x64\\libwinpthread-1.dll"
    ];

    public MainForm(bool voiceTestMode = false)
    {
        _voiceTestMode = voiceTestMode;
        Text = voiceTestMode ? "VoiceReady - voice test mode (game not attached)" : "VoiceReady";
        MinimumSize = new Size(720, 560);
        DoubleBuffered = true;
        Size = new Size(1120, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = AppBackground;
        KeyPreview = true;

        BuildLayout();
        Load += OnLoad;
        FormClosing += OnFormClosing;
        KeyDown += OnKeybindKeyDown;

        _stateTimer.Interval = 50;
        _stateTimer.Tick += (_, _) => RefreshRuntimeState();
    }

    private void BuildLayout()
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = AppBackground
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 238));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(shell);

        shell.Controls.Add(BuildSidebar(), 0, 0);

        _contentHost.Dock = DockStyle.Fill;
        _contentHost.BackColor = AppBackground;
        _contentHost.Padding = new Padding(24);
        shell.Controls.Add(_contentHost, 1, 0);

        ShowPage("dashboard");
    }

    private Control BuildSidebar()
    {
        var sidebar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = SidebarBackground,
            Padding = new Padding(18, 22, 18, 18)
        };
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var brand = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 28)
        };
        brand.Controls.Add(new Label
        {
            Text = "VOICE READY",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold),
            ForeColor = Accent,
            Margin = new Padding(1, 0, 0, 2)
        });
        brand.Controls.Add(new Label
        {
            Text = "VoiceReady",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 20, FontStyle.Bold),
            ForeColor = TextPrimary,
            Margin = new Padding(0, 0, 0, 4)
        });
        brand.Controls.Add(new Label
        {
            Text = "Ready or Not command console",
            AutoSize = true,
            ForeColor = TextSecondary,
            Margin = new Padding(1, 0, 0, 0)
        });
        sidebar.Controls.Add(brand, 0, 0);

        var nav = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        nav.Controls.Add(CreateNavButton("dashboard", "Dashboard"));
        nav.Controls.Add(CreateNavButton("audio", "Audio"));
        nav.Controls.Add(CreateNavButton("keybinds", "Keybinds"));
        nav.Controls.Add(CreateNavButton("debug", "Debug"));
        sidebar.Controls.Add(nav, 0, 1);

        _startStopButton.Text = "Start VoiceReady";
        _startStopButton.Height = 46;
        _startStopButton.Dock = DockStyle.Bottom;
        _startStopButton.Margin = new Padding(0, 18, 0, 0);
        StylePrimaryButton(_startStopButton);
        _startStopButton.Click += StartStopClicked;
        sidebar.Controls.Add(_startStopButton, 0, 2);

        return sidebar;
    }

    private Button CreateNavButton(string pageId, string text)
    {
        var button = new Button
        {
            Text = text,
            Height = 42,
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0),
            Margin = new Padding(0, 0, 0, 8),
            FlatStyle = FlatStyle.Flat,
            BackColor = SidebarBackground,
            ForeColor = TextSecondary,
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            Tag = pageId
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = PanelBackgroundAlt;
        button.FlatAppearance.MouseDownBackColor = PanelHover;
        button.Cursor = Cursors.Hand;
        button.Paint += (_, eventArgs) =>
        {
            if (pageId.Equals(_activePage, StringComparison.OrdinalIgnoreCase))
            {
                using var bar = new SolidBrush(Accent);
                eventArgs.Graphics.FillRectangle(bar, 0, 8, 3, button.Height - 16);
            }
        };
        button.Click += (_, _) => ShowPage(pageId);
        _navButtons[pageId] = button;
        return button;
    }

    private void ShowPage(string pageId)
    {
        _activePage = pageId;
        _contentHost.Controls.Clear();

        var page = pageId switch
        {
            "audio" => BuildAudioPage(),
            "keybinds" => BuildKeybindsPage(),
            "debug" => BuildDebugPage(),
            _ => BuildDashboardPage()
        };

        _contentHost.Controls.Add(page);
        UpdateNavButtons();
        SetControlsRunning(_runtime is not null);
    }

    private void UpdateNavButtons()
    {
        foreach (var (pageId, button) in _navButtons)
        {
            var active = pageId.Equals(_activePage, StringComparison.OrdinalIgnoreCase);
            button.BackColor = active ? AccentMuted : SidebarBackground;
            button.ForeColor = active ? TextPrimary : TextSecondary;
            button.Invalidate();
        }
    }

    private Control BuildDashboardPage()
    {
        var page = CreatePage("Dashboard", "Monitor recognition, memory state, and command execution.");
        var layout = (TableLayoutPanel)page.Controls[0];
        var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 2, Margin = new Padding(0) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.Controls.Add(grid);

        var statusCard = CreateCard("Runtime");
        var statusLayout = (TableLayoutPanel)statusCard.Controls[0];
        if (string.IsNullOrWhiteSpace(_statusLabel.Text))
        {
            _statusLabel.Text = "Stopped";
        }
        _statusLabel.Font = new Font(Font.FontFamily, 22, FontStyle.Bold);
        _statusLabel.ForeColor = _runtime is null ? Danger : Success;
        _statusLabel.AutoSize = true;
        statusLayout.Controls.Add(_statusLabel);

        _levelMeter.Minimum = 0;
        _levelMeter.Maximum = 100;
        _levelMeter.Height = 14;
        _levelMeter.Dock = DockStyle.Top;
        _levelMeter.Margin = new Padding(0, 18, 0, 4);
        statusLayout.Controls.Add(_levelMeter);
        statusLayout.Controls.Add(CreateMutedLabel("Microphone level"));

        var commandCard = CreateCard("Last command");
        var commandLayout = (TableLayoutPanel)commandCard.Controls[0];
        if (string.IsNullOrWhiteSpace(_speechLabel.Text))
        {
            _speechLabel.Text = "Heard: none";
        }

        if (string.IsNullOrWhiteSpace(_voiceResultLabel.Text))
        {
            _voiceResultLabel.Text = "Command: waiting";
        }
        ConfigureStatusLabel(_speechLabel, 42);
        ConfigureStatusLabel(_voiceResultLabel, 42);
        commandLayout.Controls.Add(_speechLabel);
        commandLayout.Controls.Add(_voiceResultLabel);

        var stateCard = CreateCard("Game state");
        var stateLayout = (TableLayoutPanel)stateCard.Controls[0];
        if (string.IsNullOrWhiteSpace(_menuStateLabel.Text))
        {
            _menuStateLabel.Text = "Menu: not connected";
        }

        if (string.IsNullOrWhiteSpace(_teamStateLabel.Text))
        {
            _teamStateLabel.Text = "Team: not connected";
        }
        ConfigureStatusLabel(_menuStateLabel, 42);
        ConfigureStatusLabel(_teamStateLabel, 42);
        stateLayout.Controls.Add(_menuStateLabel);
        stateLayout.Controls.Add(_teamStateLabel);

        var quickCard = CreateCard("Quick setup");
        var quickLayout = (TableLayoutPanel)quickCard.Controls[0];
        quickLayout.Controls.Add(CreateMutedLabel("Use the Audio menu for microphone and threshold tuning. Use Keybinds to match custom Ready or Not controls."));
        quickLayout.Controls.Add(BuildGameExecutableRow());
        quickLayout.Controls.Add(CreatePageLinkButton("Open Audio", "audio"));
        quickLayout.Controls.Add(CreatePageLinkButton("Open Keybinds", "keybinds"));

        Control[] cards = [statusCard, commandCard, stateCard, quickCard];
        ArrangeDashboard(grid, cards);
        layout.Resize += (_, _) => ArrangeDashboard(grid, cards);
        return page;
    }

    /// <summary>Two columns when there is room, one column on narrow windows.</summary>
    private static void ArrangeDashboard(TableLayoutPanel grid, Control[] cards)
    {
        var columns = grid.Parent is { ClientSize.Width: < 700 } ? 1 : 2;
        if (grid.ColumnCount == columns && grid.Controls.Count == cards.Length)
        {
            return;
        }

        grid.SuspendLayout();
        grid.Controls.Clear();
        grid.ColumnStyles.Clear();
        grid.RowStyles.Clear();
        grid.ColumnCount = columns;
        grid.RowCount = (cards.Length + columns - 1) / columns;
        for (var column = 0; column < columns; column++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
        }

        for (var index = 0; index < cards.Length; index++)
        {
            var column = index % columns;
            cards[index].Margin = new Padding(column == 0 ? 0 : 8, 0, column == columns - 1 ? 0 : 8, 16);
            grid.Controls.Add(cards[index], column, index / columns);
        }

        grid.ResumeLayout();
    }

    private Control BuildGameExecutableRow()
    {
        var group = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 2, AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
        group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _gameExecutableLabel.AutoSize = true;
        _gameExecutableLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _gameExecutableLabel.ForeColor = TextSecondary;
        _gameExecutableLabel.Margin = new Padding(0, 0, 0, 6);
        UpdateGameExecutableLabel();
        group.Controls.Add(_gameExecutableLabel, 0, 0);
        group.SetColumnSpan(_gameExecutableLabel, 2);

        var chooseButton = new Button { Text = "Choose game .exe", Height = 38, Dock = DockStyle.Top, Margin = new Padding(0, 0, 6, 0) };
        StyleSecondaryButton(chooseButton);
        chooseButton.Click += ChooseGameExecutableClicked;
        group.Controls.Add(chooseButton, 0, 1);

        var autoButton = new Button { Text = "Auto-detect", Height = 38, Dock = DockStyle.Top, Margin = new Padding(6, 0, 0, 0) };
        StyleSecondaryButton(autoButton);
        autoButton.Click += (_, _) => SetGameExecutable(string.Empty);
        group.Controls.Add(autoButton, 1, 1);
        return group;
    }

    private void ChooseGameExecutableClicked(object? sender, EventArgs eventArgs)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select the Ready or Not game executable",
            Filter = "Game executable (*.exe)|*.exe",
            CheckFileExists = true
        };
        var current = _baseSettings.Game.ExecutablePath;
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(Path.GetDirectoryName(current)))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(current);
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SetGameExecutable(dialog.FileName);
        }
    }

    private void SetGameExecutable(string executablePath)
    {
        _baseSettings = ReplaceGameSettings(_baseSettings, new GameSettings { ExecutablePath = executablePath });
        PersistSettings();
        UpdateGameExecutableLabel();
        LogDiagnostic(string.IsNullOrWhiteSpace(executablePath)
            ? "Game executable set to auto-detect."
            : $"Game executable set to {executablePath}.");
    }

    private void UpdateGameExecutableLabel()
    {
        var path = _baseSettings.Game.ExecutablePath;
        _gameExecutableLabel.Text = string.IsNullOrWhiteSpace(path)
            ? "Game: auto-detect"
            : $"Game: {Path.GetFileName(path)}";
    }

    private Control BuildAudioPage()
    {
        var page = CreatePage("Audio", "Choose the microphone and tune voice capture behavior.");
        var layout = (TableLayoutPanel)page.Controls[0];
        var card = CreateCard("Microphone and threshold");
        var cardLayout = (TableLayoutPanel)card.Controls[0];

        AddLabel(cardLayout, "Microphone");
        var deviceRow = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true };
        deviceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        deviceRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _microphoneCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _microphoneCombo.Dock = DockStyle.Top;
        StyleComboBox(_microphoneCombo);
        _refreshDevicesButton.Text = "Refresh";
        _refreshDevicesButton.AutoSize = true;
        StyleSecondaryButton(_refreshDevicesButton);
        _refreshDevicesButton.Click -= RefreshDevicesClicked;
        _refreshDevicesButton.Click += RefreshDevicesClicked;
        deviceRow.Controls.Add(_microphoneCombo, 0, 0);
        deviceRow.Controls.Add(_refreshDevicesButton, 1, 0);
        cardLayout.Controls.Add(deviceRow);

        _automaticThresholdCheck.Text = "Automatic speech threshold";
        _automaticThresholdCheck.AutoSize = true;
        _automaticThresholdCheck.Margin = new Padding(0, 14, 0, 4);
        _automaticThresholdCheck.ForeColor = TextPrimary;
        _automaticThresholdCheck.CheckedChanged -= ThresholdCheckedChanged;
        _automaticThresholdCheck.CheckedChanged += ThresholdCheckedChanged;
        cardLayout.Controls.Add(_automaticThresholdCheck);

        AddLabel(cardLayout, "Voice trigger threshold");
        _startThresholdSlider.AccentColor = Accent;
        _startThresholdSlider.Dock = DockStyle.Top;
        _startThresholdSlider.Height = 38;
        _startThresholdSlider.ValueChanged -= ThresholdValueChanged;
        _startThresholdSlider.ValueChanged += ThresholdValueChanged;
        cardLayout.Controls.Add(_startThresholdSlider);
        _startThresholdValueLabel.AutoSize = true;
        _startThresholdValueLabel.ForeColor = TextSecondary;
        cardLayout.Controls.Add(_startThresholdValueLabel);

        _endThresholdValueLabel.AutoSize = true;
        _endThresholdValueLabel.ForeColor = TextSecondary;
        cardLayout.Controls.Add(_endThresholdValueLabel);

        var timingGrid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true };
        timingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        timingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        AddNumericSetting(timingGrid, "Minimum speech ms", _minimumSpeechInput);
        AddNumericSetting(timingGrid, "Silence to finish ms", _trailingSilenceInput);
        cardLayout.Controls.Add(timingGrid);

        _microphoneTestButton.Text = "Test microphone";
        _microphoneTestButton.Height = 38;
        _microphoneTestButton.Dock = DockStyle.Top;
        _microphoneTestButton.Margin = new Padding(0, 18, 0, 8);
        StyleSecondaryButton(_microphoneTestButton);
        _microphoneTestButton.Click -= MicrophoneTestClicked;
        _microphoneTestButton.Click += MicrophoneTestClicked;
        cardLayout.Controls.Add(_microphoneTestButton);

        _microphoneTestLevelMeter.Minimum = 0;
        _microphoneTestLevelMeter.Maximum = 100;
        _microphoneTestLevelMeter.Height = 14;
        _microphoneTestLevelMeter.Dock = DockStyle.Top;
        _microphoneTestLevelMeter.Margin = new Padding(0, 0, 0, 6);
        cardLayout.Controls.Add(_microphoneTestLevelMeter);
        cardLayout.Controls.Add(CreateMutedLabel("Mic test level"));

        _microphoneTestLabel.Text = "Mic test: stopped";
        _microphoneTestLabel.AutoSize = false;
        _microphoneTestLabel.Height = 72;
        _microphoneTestLabel.Dock = DockStyle.Top;
        _microphoneTestLabel.ForeColor = TextPrimary;
        _microphoneTestLabel.BackColor = PanelBackgroundAlt;
        _microphoneTestLabel.Padding = new Padding(12, 10, 12, 0);
        _microphoneTestLabel.Margin = new Padding(0, 0, 0, 4);
        cardLayout.Controls.Add(_microphoneTestLabel);

        layout.Controls.Add(card);

        UpdateThresholdLabels();
        UpdateThresholdControls();
        return page;
    }

    private Control BuildKeybindsPage()
    {
        var page = CreatePage("Keybinds", "Remap VoiceReady to match customized Ready or Not command-menu controls.");
        var layout = (TableLayoutPanel)page.Controls[0];
        layout.Controls.Add(BuildKeybindSection());
        UpdateKeybindRows();
        return page;
    }

    private Control BuildDebugPage()
    {
        var page = CreatePage("Debug", "Inspect runtime logs and memory-reader details.", fillBody: true);
        var layout = (TableLayoutPanel)page.Controls[0];
        var card = CreateCard("Event log", fill: true);
        var cardLayout = (TableLayoutPanel)card.Controls[0];

        _debugCheck.Text = "Show log output";
        _debugCheck.AutoSize = true;
        _debugCheck.Checked = true;
        _debugCheck.ForeColor = TextPrimary;
        _debugCheck.Margin = new Padding(0, 0, 0, 10);
        _debugCheck.CheckedChanged -= DebugVisibilityChanged;
        _debugCheck.CheckedChanged += DebugVisibilityChanged;
        cardLayout.Controls.Add(_debugCheck);

        _debugLog.Dock = DockStyle.Fill;
        _debugLog.ReadOnly = true;
        _debugLog.BorderStyle = BorderStyle.None;
        _debugLog.BackColor = PanelBackgroundAlt;
        _debugLog.ForeColor = Color.FromArgb(218, 218, 222);
        _debugLog.Font = new Font("Cascadia Mono", 9);
        UseDarkScrollbars(_debugLog);
        _debugLog.Visible = _debugCheck.Checked;
        cardLayout.Controls.Add(_debugLog);
        layout.Controls.Add(card);

        return page;
    }

    private void DebugVisibilityChanged(object? sender, EventArgs eventArgs)
    {
        _debugLog.Visible = _debugCheck.Checked;
    }

    private Panel CreatePage(string heading, string subtitle, bool fillBody = false)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground
        };

        // Content pages scroll when the window is too small, so controls never end up outside the frame.
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoScroll = !fillBody,
            Padding = new Padding(0, 0, fillBody ? 0 : 6, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(fillBody ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 20)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.Controls.Add(new Label
        {
            Text = heading,
            AutoSize = true,
            Font = new Font(Font.FontFamily, 22, FontStyle.Bold),
            ForeColor = TextPrimary,
            Margin = new Padding(0, 0, 0, 3)
        });
        header.Controls.Add(new Label
        {
            Text = subtitle,
            AutoSize = true,
            ForeColor = TextSecondary,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(1, 0, 0, 0)
        });
        layout.Controls.Add(header);
        UseDarkScrollbars(layout);
        panel.Controls.Add(layout);
        return panel;
    }

    private Panel CreateCard(string heading, bool fill = false)
    {
        var panel = new CardPanel
        {
            Dock = fill ? DockStyle.Fill : DockStyle.Top,
            AutoSize = !fill,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppBackground,
            CardColor = PanelBackground,
            BorderColor = BorderColor,
            Padding = new Padding(20, 18, 20, 18),
            Margin = new Padding(0, 0, 0, 16)
        };

        var layout = new TableLayoutPanel
        {
            Dock = fill ? DockStyle.Fill : DockStyle.Top,
            AutoSize = !fill,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 1,
            AutoScroll = false,
            BackColor = PanelBackground
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label
        {
            Text = heading.ToUpperInvariant(),
            AutoSize = true,
            Font = new Font(Font.FontFamily, 9, FontStyle.Bold),
            ForeColor = TextSecondary,
            Margin = new Padding(0, 0, 0, 12)
        });
        panel.Controls.Add(layout);
        return panel;
    }

    private static void AddLabel(TableLayoutPanel layout, string text)
    {
        layout.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = TextSecondary,
            Margin = new Padding(0, 10, 0, 4)
        });
    }

    private static void AddNumericSetting(TableLayoutPanel layout, string label, DarkNumberInput input)
    {
        var group = new TableLayoutPanel { Dock = DockStyle.Top, RowCount = 2, AutoSize = true, Margin = new Padding(0, 12, 8, 0) };
        group.Controls.Add(new Label { Text = label, AutoSize = true, ForeColor = TextSecondary });
        input.Dock = DockStyle.Top;
        input.BackColor = PanelBackgroundAlt;
        input.ForeColor = TextPrimary;
        group.Controls.Add(input);
        layout.Controls.Add(group);
    }

    // Left|Right anchoring makes AutoSize labels wrap to the column width instead of overflowing it.
    private static void UseDarkScrollbars(Control control)
    {
        control.HandleCreated += (_, _) => SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr handle, string? subAppName, string? subIdList);

    private static Label CreateMutedLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = TextSecondary,
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
        Margin = new Padding(0, 4, 0, 8)
    };

    private Button CreatePageLinkButton(string text, string pageId)
    {
        var button = new Button
        {
            Text = text,
            Height = 38,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 12, 0, 0)
        };
        StyleSecondaryButton(button);
        button.Click += (_, _) => ShowPage(pageId);
        return button;
    }

    private static void ConfigureStatusLabel(Label label, int height)
    {
        label.AutoSize = true;
        label.MinimumSize = new Size(0, height);
        label.Dock = DockStyle.None;
        label.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        label.ForeColor = TextPrimary;
        label.BackColor = PanelBackgroundAlt;
        label.Padding = new Padding(12, 10, 12, 10);
        label.Margin = new Padding(0, 0, 0, 10);
    }

    private static void StyleInput(Control control)
    {
        control.BackColor = PanelBackgroundAlt;
        control.ForeColor = TextPrimary;
    }

    private void StyleComboBox(ComboBox comboBox)
    {
        comboBox.BackColor = PanelBackgroundAlt;
        comboBox.ForeColor = TextPrimary;
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.DrawMode = DrawMode.OwnerDrawFixed;
        comboBox.ItemHeight = 28;
        comboBox.DrawItem -= MicrophoneComboDrawItem;
        comboBox.DrawItem += MicrophoneComboDrawItem;
    }

    private void MicrophoneComboDrawItem(object? sender, DrawItemEventArgs eventArgs)
    {
        if (sender is not ComboBox comboBox || eventArgs.Index < 0)
        {
            return;
        }

        var selected = (eventArgs.State & DrawItemState.Selected) == DrawItemState.Selected;
        using var background = new SolidBrush(selected ? AccentMuted : PanelBackgroundAlt);
        using var foreground = new SolidBrush(TextPrimary);
        eventArgs.Graphics.FillRectangle(background, eventArgs.Bounds);
        eventArgs.Graphics.DrawString(comboBox.Items[eventArgs.Index]?.ToString() ?? string.Empty, comboBox.Font, foreground, eventArgs.Bounds.X + 8, eventArgs.Bounds.Y + 5);
        eventArgs.DrawFocusRectangle();
    }

    private static void StylePrimaryButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = Accent;
        button.ForeColor = Color.White;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = AccentHover;
        button.FlatAppearance.MouseDownBackColor = Accent;
        button.Cursor = Cursors.Hand;
        button.Font = new Font(button.Font.FontFamily, button.Font.Size, FontStyle.Bold);
    }

    private static void StyleSecondaryButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = PanelBackgroundAlt;
        button.ForeColor = TextPrimary;
        button.FlatAppearance.BorderColor = BorderColor;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = PanelHover;
        button.FlatAppearance.MouseDownBackColor = AccentMuted;
        button.Cursor = Cursors.Hand;
    }

    private async void StartStopClicked(object? sender, EventArgs eventArgs)
    {
        await ToggleRuntimeAsync();
    }

    private void RefreshDevicesClicked(object? sender, EventArgs eventArgs)
    {
        LoadMicrophones();
    }

    private void ThresholdCheckedChanged(object? sender, EventArgs eventArgs)
    {
        UpdateThresholdControls();
    }

    private void ThresholdValueChanged(object? sender, EventArgs eventArgs)
    {
        UpdateThresholdLabels();
    }

    private void ResetAllClicked(object? sender, EventArgs eventArgs)
    {
        ResetAllKeybinds();
    }

    private async void MicrophoneTestClicked(object? sender, EventArgs eventArgs)
    {
        if (_microphoneTestTask is null)
        {
            await StartMicrophoneTestAsync();
        }
        else
        {
            await StopMicrophoneTestAsync();
        }
    }

    private void OnLoad(object? sender, EventArgs eventArgs)
    {
        _repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        _configDir = Path.Combine(_repoRoot, "config");
        Directory.CreateDirectory(_configDir);
        _diagnosticLogPath = Path.Combine(_configDir, "voice_ready_diagnostics.log");
        File.WriteAllText(_diagnosticLogPath, $"VoiceReady diagnostics started {DateTimeOffset.Now:O}{Environment.NewLine}");
        LogDiagnostic($"Repo root: {_repoRoot}");
        LogDiagnostic($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        LogDiagnostic($".NET: {Environment.Version}");
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
        _voiceSettingsPath = voiceSettingsPath;
        _baseSettings = ReplaceInputSettings(_baseSettings, NormalizeInputSettings(_baseSettings.Input));

        // Ranges must be set before loading values, otherwise the saved values are clamped to the
        // controls' default 0..100 range (e.g. a -35 dB threshold becomes 0 dB and no speech is ever detected).
        _automaticThresholdCheck.Checked = true;
        _startThresholdSlider.Minimum = -70;
        _startThresholdSlider.Maximum = -15;
        _minimumSpeechInput.Minimum = 100;
        _minimumSpeechInput.Maximum = 1500;
        _minimumSpeechInput.Increment = 25;
        _trailingSilenceInput.Minimum = 150;
        _trailingSilenceInput.Maximum = 2000;
        _trailingSilenceInput.Increment = 25;
        _startThresholdSlider.Value = Clamp((int)Math.Round(_baseSettings.Audio.SpeechStartDb), _startThresholdSlider.Minimum, _startThresholdSlider.Maximum);
        _minimumSpeechInput.Value = Clamp(_baseSettings.Audio.MinimumSpeechMilliseconds, (int)_minimumSpeechInput.Minimum, (int)_minimumSpeechInput.Maximum);
        _trailingSilenceInput.Value = Clamp(_baseSettings.Audio.TrailingSilenceMilliseconds, (int)_trailingSilenceInput.Minimum, (int)_trailingSilenceInput.Maximum);
        UpdateKeybindRows();
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

        if (_microphoneTestTask is not null)
        {
            await StopMicrophoneTestAsync();
        }

        SetControlsRunning(true);
        _statusLabel.Text = "Starting";
        _statusLabel.ForeColor = Warning;
        _debugLog.Clear();
        _lastMenuValue = null;
        _lastTeamValue = null;
        _hasLoggedRoots = false;

        try
        {
            LogDiagnostic("Start VoiceReady requested.");
            _shutdown = new CancellationTokenSource();
            var settings = BuildRuntimeSettings();
            ValidateStartupAssets(settings);
            LogDiagnostic("Startup assets validated.");
            if (_voiceTestMode)
            {
                _runtime = new RuntimeContext(null, null, null, null, new VoiceCommandParser(), null, settings);
                LogDebug("Voice test mode: Ready or Not is not attached and no input is sent. Commands are only parsed.");
                _menuStateLabel.Text = "Menu: voice test mode (game not attached)";
                _teamStateLabel.Text = "Team: voice test mode (game not attached)";
                _runtime.VoiceTask = Task.Run(() => RunVoiceLoopAsync(_runtime, _shutdown.Token));
                _statusLabel.Text = "Running";
                _statusLabel.ForeColor = Success;
                _startStopButton.Text = "Stop VoiceReady";
                return;
            }

            var processReader = ProcessMemoryReader.AttachToGame(settings.Game.ExecutablePath, _memoryMap.ProcessNames, _memoryMap.ProcessNamePattern);
            LogDiagnostic($"Attached to {processReader.ProcessName} ({processReader.ProcessId}).");
            CommandMenuReflectionReader? reflectionReader = null;
            if (_memoryMap.Reflection.Enabled)
            {
                try
                {
                    reflectionReader = CommandMenuReflectionReader.Create(processReader, _memoryMap.Reflection, _memoryMap.MenuState.KnownStates);
                    LogDiagnostic("Reading menu and team state through Unreal reflection.");
                }
                catch (Exception ex)
                {
                    LogDiagnostic($"Unreal reflection unavailable, using pointer paths: {ex.Message}");
                }
            }

            var menuReader = new MenuStateReader(processReader, _memoryMap.MenuState.PointerPaths, reflectionReader is null ? null : reflectionReader.ReadMenuState);
            var teamSelectionReader = new MenuStateReader(processReader, _memoryMap.TeamSelection.PointerPaths, reflectionReader is null ? null : reflectionReader.ReadTeamSelection);
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
            LogDebug($"Fallback: {_memoryMap.MenuState.PointerPaths.Count} menu-state pointer paths.");
            LogDebug($"Fallback: {_memoryMap.TeamSelection.PointerPaths.Count} team-selection pointer paths.");

            LogDiagnostic("Starting voice loop task.");
            _runtime.VoiceTask = Task.Run(() => RunVoiceLoopAsync(_runtime, _shutdown.Token));
            _stateTimer.Start();
            _statusLabel.Text = "Running";
            _statusLabel.ForeColor = Success;
            _startStopButton.Text = "Stop VoiceReady";
        }
        catch (Exception ex)
        {
            LogDebug($"Startup error: {ex.Message}");
            _statusLabel.Text = "Could not start";
            _statusLabel.ForeColor = Danger;
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
        _statusLabel.ForeColor = Danger;
        _startStopButton.Text = "Start VoiceReady";
        SetControlsRunning(false);
    }

    private async Task StartMicrophoneTestAsync()
    {
        if (_runtime is not null)
        {
            return;
        }

        _microphoneTestShutdown = new CancellationTokenSource();
        var settings = BuildRuntimeSettings();
        try
        {
            LogDiagnostic("Microphone test requested.");
            ValidateStartupAssets(settings);
        }
        catch (Exception ex)
        {
            _microphoneTestShutdown.Dispose();
            _microphoneTestShutdown = null;
            _microphoneTestLabel.Text = $"Mic test error: {ex.Message}";
            MessageBox.Show(this, ex.Message, "VoiceReady microphone test failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _lastMicrophoneTestTranscript = "none";
        _microphoneTestButton.Text = "Stop microphone test";
        _microphoneTestLevelMeter.Value = 0;
        _microphoneTestLabel.Text = "Mic test: listening";
        _speechLabel.Text = "Heard: mic test started";
        LogDebug("Microphone test started.");
        _microphoneTestTask = Task.Run(() => RunMicrophoneTestLoopAsync(settings, _microphoneTestShutdown.Token));
        SetControlsRunning(false);
        try
        {
            await Task.CompletedTask;
        }
        catch
        {
            // Start is fire-and-forget; loop errors are reported through the UI.
        }
    }

    private async Task StopMicrophoneTestAsync()
    {
        _microphoneTestShutdown?.Cancel();

        var task = _microphoneTestTask;
        _microphoneTestTask = null;
        if (task is not null)
        {
            try
            {
                await task.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Audio capture can take a moment to unwind.
            }
        }

        _microphoneTestShutdown?.Dispose();
        _microphoneTestShutdown = null;
        _microphoneTestButton.Text = "Test microphone";
        _microphoneTestLabel.Text = "Mic test: stopped";
        _microphoneTestLevelMeter.Value = 0;
        _levelMeter.Value = 0;
        LogDebug("Microphone test stopped.");
        SetControlsRunning(_runtime is not null);
    }

    private async Task RunMicrophoneTestLoopAsync(VoiceReadySettings settings, CancellationToken cancellationToken)
    {
        try
        {
            LogDiagnostic("Microphone test loop creating audio source.");
            using var audioSource = new WaveInAudioSource(settings.Audio);
            audioSource.Start();
            LogDiagnostic("Microphone test audio source started.");
            LogDiagnostic("Microphone test creating Vosk transcriber.");
            using var transcriber = new VoskTranscriber(settings.Vosk, _repoRoot, settings.Audio.SampleRate);
            LogDiagnostic("Microphone test Vosk transcriber created.");
            var segmenter = new SpeechSegmenter(settings.Audio);
            LogDebug("Microphone test Vosk recognition started.");

            while (!cancellationToken.IsCancellationRequested)
            {
                while (audioSource.TryRead(out var frame) && frame is not null)
                {
                    var segment = segmenter.Process(frame);
                    BeginInvoke(() => UpdateMicrophoneTestLevel(segmenter.CurrentDecibels, segmenter.IsSpeaking));

                    if (segment is null)
                    {
                        continue;
                    }

                    var result = transcriber.Transcribe(segment);
                    if (result is null)
                    {
                        continue;
                    }

                    BeginInvoke(() =>
                    {
                        _lastMicrophoneTestTranscript = result.Text;
                        _microphoneTestLabel.Text = $"Mic test: transcript - {result.Text}";
                        _speechLabel.Text = $"Heard: {result.Text}";
                    });
                    LogDebug($"{DateTimeOffset.Now:HH:mm:ss.fff} mic test speech=\"{result.Text}\"");
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
                _microphoneTestLabel.Text = $"Mic test error: {ex.Message}";
                _microphoneTestButton.Text = "Test microphone";
                SetControlsRunning(false);
            });
            LogDebug($"Microphone test error: {ex.Message}");
            _microphoneTestTask = null;
            _microphoneTestShutdown?.Dispose();
            _microphoneTestShutdown = null;
        }
    }

    private async Task RunVoiceLoopAsync(RuntimeContext runtime, CancellationToken cancellationToken)
    {
        try
        {
            LogDiagnostic("Voice loop creating audio source.");
            using var audioSource = new WaveInAudioSource(runtime.Settings.Audio);
            audioSource.Start();
            LogDiagnostic("Voice loop audio source started.");
            LogDebug("Microphone capture started.");

            var activeSettings = runtime.Settings;
            if (_automaticThresholdCheck.Checked)
            {
                activeSettings = await CalibrateSettingsAsync(audioSource, activeSettings, cancellationToken);
            }

            LogDiagnostic("Voice loop creating Vosk transcriber.");
            using var transcriber = new VoskTranscriber(activeSettings.Vosk, _repoRoot, activeSettings.Audio.SampleRate);
            LogDiagnostic("Voice loop Vosk transcriber created.");
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

                        if (runtime.Executor is null)
                        {
                            var steps = string.Join(" > ", plan.Steps.Select(step => $"{step.Key}:{step.Name}"));
                            var team = plan.TeamSelection is null ? string.Empty : $" team={plan.TeamSelection}";
                            BeginInvoke(() => _voiceResultLabel.Text = $"Command: would run {plan.Name} ({steps}){team}");
                            LogDebug($"{DateTimeOffset.Now:HH:mm:ss.fff} voice result=test {plan.Name} from {plan.RequiredInitialState}: {steps}{team}");
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
                _statusLabel.ForeColor = Danger;
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
        if (_runtime is not { ProcessReader: { } processReader, MenuReader: { } menuReader, TeamSelectionReader: { } teamSelectionReader } ||
            _memoryMap is null)
        {
            return;
        }

        if (!_hasLoggedRoots)
        {
            foreach (var root in processReader.RootResolutions.OrderBy(root => root.ConfiguredOffset))
            {
                var source = root.UsedSignature ? "signature" : "fallback";
                var moved = root.ConfiguredOffset == root.ResolvedOffset ? string.Empty : $" moved-from=+0x{root.ConfiguredOffset:X}";
                var reason = string.IsNullOrWhiteSpace(root.FallbackReason) ? string.Empty : $" reason={root.FallbackReason}";
                LogDebug($"Pointer root {root.ModuleName}+0x{root.ResolvedOffset:X} source={source}{moved}{reason}");
            }

            _hasLoggedRoots = true;
        }

        var snapshot = menuReader.Read();
        var teamSnapshot = teamSelectionReader.Read();
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
            var detailText = snapshot.Detail is null ? string.Empty : $", detail={snapshot.Detail}";
            var fallbackText = menuReader.FallbackReason is null ? string.Empty : $", fallback={menuReader.FallbackReason}";
            LogDebug($"{DateTimeOffset.Now:HH:mm:ss.fff} value={valueText}, source={snapshot.Source}, confidence={snapshot.Confidence:P0}, reads={snapshot.SuccessfulReads}, failed={snapshot.FailedReads}{commandText}{detailText}{fallbackText}");
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
            LogDebug($"{DateTimeOffset.Now:HH:mm:ss.fff} team={teamSnapshot.VotedValue?.ToString() ?? "no consensus"} ({teamName}), source={teamSnapshot.Source}, confidence={teamSnapshot.Confidence:P0}, reads={teamSnapshot.SuccessfulReads}, failed={teamSnapshot.FailedReads}");
            _lastTeamValue = teamSnapshot.VotedValue;
        }
    }

    private void LogFailedReads(MenuStateSnapshot snapshot)
    {
        if (snapshot.IsDirect || snapshot.SuccessfulReads != 0)
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
            Input = settings.Input,
            Game = settings.Game
        };
    }

    private Control BuildKeybindSection()
    {
        var card = CreateCard("Game keybinds");
        var group = (TableLayoutPanel)card.Controls[0];

        var header = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(new Label
        {
            Text = "Command menu inputs",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 11, FontStyle.Bold),
            ForeColor = TextPrimary,
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);

        _resetAllKeybindsButton.Text = "Reset all";
        _resetAllKeybindsButton.AutoSize = true;
        _resetAllKeybindsButton.Margin = new Padding(8, 0, 0, 8);
        StyleSecondaryButton(_resetAllKeybindsButton);
        _resetAllKeybindsButton.Click -= ResetAllClicked;
        _resetAllKeybindsButton.Click += ResetAllClicked;
        header.Controls.Add(_resetAllKeybindsButton, 1, 0);
        group.Controls.Add(header);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            AutoSize = true,
            Padding = new Padding(0, 6, 0, 0)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        group.Controls.Add(grid);

        AddKeybindRow(grid, "commandMenuOpen", "Open command menu");
        foreach (var key in new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" })
        {
            AddKeybindRow(grid, key, $"Command option {key}");
        }

        return card;
    }

    private void AddKeybindRow(TableLayoutPanel grid, string id, string label)
    {
        var row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var labelControl = new Label
        {
            Text = label,
            AutoSize = false,
            Height = 36,
            Dock = DockStyle.Fill,
            ForeColor = TextSecondary,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Margin = new Padding(0, 3, 8, 3),
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };

        var bindButton = new Button
        {
            Text = "Default",
            Width = 140,
            Height = 32,
            Margin = new Padding(0, 4, 8, 4),
            Anchor = AnchorStyles.None,
            Tag = id
        };
        StyleSecondaryButton(bindButton);
        bindButton.Click += (_, _) => BeginKeybindCapture(id);
        bindButton.MouseDown += OnKeybindMouseDown;

        var resetButton = new Button
        {
            Text = "Reset",
            AutoSize = false,
            Width = 76,
            Height = 32,
            Margin = new Padding(0, 4, 0, 4),
            Anchor = AnchorStyles.None,
            Tag = id
        };
        StyleSecondaryButton(resetButton);
        resetButton.Click += (_, _) => ResetKeybind(id);

        grid.Controls.Add(labelControl, 0, row);
        grid.Controls.Add(bindButton, 1, row);
        grid.Controls.Add(resetButton, 2, row);
        _keybindRows[id] = new KeybindRow(bindButton, resetButton);
    }

    private void BeginKeybindCapture(string id)
    {
        CancelPendingKeybindCapture();
        _pendingKeybindId = id;
        if (_keybindRows.TryGetValue(id, out var row))
        {
            row.BindButton.Text = "Press key or mouse";
            row.BindButton.Focus();
        }
    }

    private void OnKeybindKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (_pendingKeybindId is null)
        {
            return;
        }

        eventArgs.Handled = true;
        eventArgs.SuppressKeyPress = true;

        if (eventArgs.KeyCode == Keys.Escape)
        {
            CancelPendingKeybindCapture();
            return;
        }

        var scanCode = MapVirtualKey((uint)eventArgs.KeyCode, 0);
        if (scanCode == 0)
        {
            return;
        }

        SaveKeybind(_pendingKeybindId, InputBinding.Keyboard(scanCode.ToString("X2"), FormatKeyName(eventArgs.KeyCode)));
    }

    private void OnKeybindMouseDown(object? sender, MouseEventArgs eventArgs)
    {
        if (_pendingKeybindId is null)
        {
            return;
        }

        var binding = eventArgs.Button switch
        {
            MouseButtons.Middle => InputBinding.MouseMiddle(),
            MouseButtons.Right => InputBinding.MouseButton("MouseRight", "Right Mouse"),
            MouseButtons.XButton1 => InputBinding.MouseButton("MouseX1", "Mouse Button 4"),
            MouseButtons.XButton2 => InputBinding.MouseButton("MouseX2", "Mouse Button 5"),
            _ => null
        };

        if (binding is not null)
        {
            SaveKeybind(_pendingKeybindId, binding);
        }
    }

    private void SaveKeybind(string id, InputBinding binding)
    {
        var input = WithKeybind(_baseSettings.Input, id, binding);
        _baseSettings = ReplaceInputSettings(_baseSettings, input);
        PersistSettings();
        _pendingKeybindId = null;
        UpdateKeybindRows();
    }

    private void ResetKeybind(string id)
    {
        SaveKeybind(id, GetDefaultBinding(id));
    }

    private void ResetAllKeybinds()
    {
        var input = new InputSettings
        {
            CommandMenuOpen = InputBinding.MouseMiddle(),
            CommandKeys = InputSettings.CreateDefaultCommandKeys(),
            KeyHoldMilliseconds = _baseSettings.Input.KeyHoldMilliseconds,
            BetweenKeysMilliseconds = _baseSettings.Input.BetweenKeysMilliseconds,
            StateTransitionTimeoutMilliseconds = _baseSettings.Input.StateTransitionTimeoutMilliseconds,
            CloseMenuScanCode = _baseSettings.Input.CloseMenuScanCode,
            TeamSelectionWheelDelta = _baseSettings.Input.TeamSelectionWheelDelta,
            TeamSelectionMaximumScrolls = _baseSettings.Input.TeamSelectionMaximumScrolls
        };
        _baseSettings = ReplaceInputSettings(_baseSettings, input);
        PersistSettings();
        _pendingKeybindId = null;
        UpdateKeybindRows();
    }

    private void CancelPendingKeybindCapture()
    {
        _pendingKeybindId = null;
        UpdateKeybindRows();
    }

    private void UpdateKeybindRows()
    {
        foreach (var (id, row) in _keybindRows)
        {
            row.BindButton.Text = GetBinding(id).DisplayName;
        }
    }

    private InputBinding GetBinding(string id)
    {
        return id.Equals("commandMenuOpen", StringComparison.OrdinalIgnoreCase)
            ? _baseSettings.Input.CommandMenuOpen
            : _baseSettings.Input.GetCommandKey(id);
    }

    private static InputBinding GetDefaultBinding(string id)
    {
        return id.Equals("commandMenuOpen", StringComparison.OrdinalIgnoreCase)
            ? InputBinding.MouseMiddle()
            : InputSettings.GetDefaultCommandKey(id);
    }

    private static InputSettings WithKeybind(InputSettings input, string id, InputBinding binding)
    {
        var commandKeys = new Dictionary<string, InputBinding>(input.CommandKeys, StringComparer.OrdinalIgnoreCase);
        if (id.Equals("commandMenuOpen", StringComparison.OrdinalIgnoreCase))
        {
            return CopyInputSettings(input, binding, commandKeys);
        }

        commandKeys[id] = binding;
        return CopyInputSettings(input, input.CommandMenuOpen, commandKeys);
    }

    private static InputSettings NormalizeInputSettings(InputSettings input)
    {
        var commandKeys = InputSettings.CreateDefaultCommandKeys();
        foreach (var (key, binding) in input.CommandKeys)
        {
            commandKeys[key] = binding;
        }

        return CopyInputSettings(input, input.CommandMenuOpen, commandKeys);
    }

    private static InputSettings CopyInputSettings(
        InputSettings input,
        InputBinding commandMenuOpen,
        Dictionary<string, InputBinding> commandKeys) => new()
    {
        CommandMenuOpen = commandMenuOpen,
        CommandKeys = commandKeys,
        KeyHoldMilliseconds = input.KeyHoldMilliseconds,
        BetweenKeysMilliseconds = input.BetweenKeysMilliseconds,
        StateTransitionTimeoutMilliseconds = input.StateTransitionTimeoutMilliseconds,
        CloseMenuScanCode = input.CloseMenuScanCode,
        TeamSelectionWheelDelta = input.TeamSelectionWheelDelta,
        TeamSelectionMaximumScrolls = input.TeamSelectionMaximumScrolls
    };

    private static VoiceReadySettings ReplaceInputSettings(VoiceReadySettings settings, InputSettings input) => new()
    {
        Audio = settings.Audio,
        Vosk = settings.Vosk,
        Input = input,
        Game = settings.Game
    };

    private static VoiceReadySettings ReplaceGameSettings(VoiceReadySettings settings, GameSettings game) => new()
    {
        Audio = settings.Audio,
        Vosk = settings.Vosk,
        Input = settings.Input,
        Game = game
    };

    private void PersistSettings()
    {
        if (string.IsNullOrWhiteSpace(_voiceSettingsPath))
        {
            return;
        }

        VoiceReadySettingsLoader.Save(_voiceSettingsPath, _baseSettings);
    }

    private static string FormatKeyName(Keys key)
    {
        return key switch
        {
            Keys.D0 => "0",
            Keys.D1 => "1",
            Keys.D2 => "2",
            Keys.D3 => "3",
            Keys.D4 => "4",
            Keys.D5 => "5",
            Keys.D6 => "6",
            Keys.D7 => "7",
            Keys.D8 => "8",
            Keys.D9 => "9",
            Keys.Space => "Space",
            Keys.Oemtilde => "`",
            Keys.OemMinus => "-",
            Keys.Oemplus => "=",
            Keys.OemOpenBrackets => "[",
            Keys.OemCloseBrackets => "]",
            Keys.OemPipe => "\\",
            Keys.OemSemicolon => ";",
            Keys.OemQuotes => "'",
            Keys.Oemcomma => ",",
            Keys.OemPeriod => ".",
            Keys.OemQuestion => "/",
            _ => key.ToString()
        };
    }

    private void UpdateLevel(double decibels, bool speaking)
    {
        var value = Clamp((int)Math.Round((decibels + 70) / 55 * 100), 0, 100);
        _levelMeter.Value = value;
        _levelMeter.Active = speaking;
        _statusLabel.Text = _runtime is null ? "Stopped" : speaking ? "Listening" : "Running";
        _statusLabel.ForeColor = _runtime is null ? Danger : speaking ? Accent : Success;
    }

    private void UpdateMicrophoneTestLevel(double decibels, bool speaking)
    {
        var value = Clamp((int)Math.Round((decibels + 70) / 55 * 100), 0, 100);
        _levelMeter.Value = value;
        _microphoneTestLevelMeter.Value = value;
        _microphoneTestLevelMeter.Active = speaking;
        var state = speaking ? "hearing speech" : "listening";
        _microphoneTestLabel.Text = $"Mic test: {state}\nLast transcript: {_lastMicrophoneTestTranscript}";
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
        var testingMicrophone = _microphoneTestTask is not null;
        var editable = !running && !testingMicrophone;
        _microphoneCombo.Enabled = editable;
        _refreshDevicesButton.Enabled = editable;
        _automaticThresholdCheck.Enabled = editable;
        _startThresholdSlider.Enabled = editable && !_automaticThresholdCheck.Checked;
        _minimumSpeechInput.Enabled = editable;
        _trailingSilenceInput.Enabled = editable;
        _microphoneTestButton.Enabled = !running;
        foreach (var row in _keybindRows.Values)
        {
            row.BindButton.Enabled = !running;
            row.ResetButton.Enabled = !running;
        }
        _resetAllKeybindsButton.Enabled = !running;

        if (running)
        {
            _pendingKeybindId = null;
            UpdateKeybindRows();
        }

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
        LogDiagnostic(message);
    }

    private void LogDiagnostic(string message)
    {
        if (string.IsNullOrWhiteSpace(_diagnosticLogPath))
        {
            return;
        }

        try
        {
            File.AppendAllText(_diagnosticLogPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostic logging must never block the UI or command execution.
        }
    }

    private void ValidateStartupAssets(VoiceReadySettings settings)
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new InvalidOperationException(
                $"VoiceReady currently bundles x64 Vosk native files, but the app is running as {RuntimeInformation.ProcessArchitecture}. Install the x64 .NET SDK and run VoiceReady from an x64 terminal.");
        }

        foreach (var relativePath in RequiredVoskNativeFiles)
        {
            var path = Path.Combine(_repoRoot, relativePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Required Vosk native file is missing: {path}");
            }
        }

        var modelPath = Path.IsPathRooted(settings.Vosk.ModelPath)
            ? settings.Vosk.ModelPath
            : Path.GetFullPath(Path.Combine(_repoRoot, settings.Vosk.ModelPath));
        if (!Directory.Exists(modelPath))
        {
            throw new DirectoryNotFoundException($"Vosk model folder is missing: {modelPath}");
        }

        foreach (var relativePath in RequiredVoskModelFiles)
        {
            var path = Path.Combine(modelPath, relativePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Required Vosk model file is missing: {path}");
            }

            if (new FileInfo(path).Length == 0)
            {
                throw new InvalidOperationException($"Required Vosk model file is empty: {path}");
            }
        }

        LogDiagnostic($"Vosk assets OK. Model={modelPath}");
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (_runtime is null && _microphoneTestTask is null)
        {
            return;
        }

        eventArgs.Cancel = true;
        if (_microphoneTestTask is not null)
        {
            await StopMicrophoneTestAsync();
        }

        if (_runtime is not null)
        {
            await StopRuntimeAsync();
        }

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

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    private sealed class DeviceItem(AudioInputDevice device)
    {
        public AudioInputDevice Device { get; } = device;

        public override string ToString() => $"{Device.Name} ({Device.DeviceNumber})";
    }

    private sealed record KeybindRow(Button BindButton, Button ResetButton);

    /// <summary>Card with rounded corners and a hairline border.</summary>
    private sealed class CardPanel : Panel
    {
        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color CardColor { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor { get; set; }

        public override Size GetPreferredSize(Size proposedSize)
        {
            if (Controls.Count == 0 || Dock == DockStyle.Fill)
            {
                return base.GetPreferredSize(proposedSize);
            }

            // Measure at the width the card actually has: a parent TableLayoutPanel proposes the whole
            // grid width, which under-counts wrapped text in a narrower column.
            var width = Width > 0 ? Width : proposedSize.Width;
            var content = Controls[0].GetPreferredSize(new Size(Math.Max(1, width - Padding.Horizontal), 0));
            return new Size(width, content.Height + Padding.Vertical);
        }

        private int _lastWidth;

        protected override void OnSizeChanged(EventArgs eventArgs)
        {
            base.OnSizeChanged(eventArgs);
            if (Width != _lastWidth)
            {
                _lastWidth = Width;
                Parent?.PerformLayout();
            }
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            eventArgs.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 10);
            using var fill = new SolidBrush(CardColor);
            using var border = new Pen(BorderColor);
            eventArgs.Graphics.FillPath(fill, path);
            eventArgs.Graphics.DrawPath(border, path);
        }
    }

    /// <summary>Flat audio level bar; turns green while speech is detected.</summary>
    private sealed class LevelMeter : Control
    {
        private int _value;
        private bool _active;

        public LevelMeter()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Minimum { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Maximum { get; set; } = 100;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Value
        {
            get => _value;
            set
            {
                var next = Math.Clamp(value, Minimum, Maximum);
                if (next != _value)
                {
                    _value = next;
                    Invalidate();
                }
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Active
        {
            get => _active;
            set
            {
                if (value != _active)
                {
                    _active = value;
                    Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            var graphics = eventArgs.Graphics;
            graphics.Clear(Parent?.BackColor ?? PanelBackground);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var radius = Math.Max(2, Height / 2);
            using (var track = RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), radius))
            using (var trackBrush = new SolidBrush(PanelBackgroundAlt))
            {
                graphics.FillPath(trackBrush, track);
            }

            var range = Math.Max(1, Maximum - Minimum);
            var width = (int)Math.Round((Width - 1) * (double)(_value - Minimum) / range);
            if (width >= Height)
            {
                using var bar = RoundedRectangle(new Rectangle(0, 0, width, Height - 1), radius);
                using var barBrush = new SolidBrush(_active ? Success : Accent);
                graphics.FillPath(barBrush, bar);
            }
        }
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class DarkSlider : Control
    {
        private bool _dragging;
        private int _minimum;
        private int _maximum = 100;
        private int _value;

        public event EventHandler? ValueChanged;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Minimum
        {
            get => _minimum;
            set
            {
                _minimum = value;
                if (_maximum < _minimum)
                {
                    _maximum = _minimum;
                }

                Value = _value;
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Maximum
        {
            get => _maximum;
            set
            {
                _maximum = Math.Max(value, _minimum);
                Value = _value;
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Value
        {
            get => _value;
            set
            {
                var next = Math.Min(Math.Max(value, _minimum), _maximum);
                if (_value == next)
                {
                    return;
                }

                _value = next;
                ValueChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color AccentColor { get; set; } = Accent;

        public DarkSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Height = 38;
            BackColor = PanelBackground;
            ForeColor = TextPrimary;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            var graphics = eventArgs.Graphics;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var trackLeft = 10;
            var trackRight = Width - 10;
            var trackWidth = Math.Max(1, trackRight - trackLeft);
            var y = Height / 2;
            var ratio = _maximum == _minimum ? 0 : (double)(_value - _minimum) / (_maximum - _minimum);
            var thumbX = trackLeft + (int)Math.Round(trackWidth * ratio);

            using var trackBrush = new SolidBrush(PanelBackgroundAlt);
            using var fillBrush = new SolidBrush(AccentColor);
            using var thumbBrush = new SolidBrush(TextPrimary);
            graphics.FillRectangle(trackBrush, trackLeft, y - 3, trackWidth, 6);
            graphics.FillRectangle(fillBrush, trackLeft, y - 3, Math.Max(0, thumbX - trackLeft), 6);
            graphics.FillEllipse(thumbBrush, thumbX - 7, y - 7, 14, 14);
        }

        protected override void OnMouseDown(MouseEventArgs eventArgs)
        {
            base.OnMouseDown(eventArgs);
            _dragging = true;
            Capture = true;
            SetValueFromX(eventArgs.X);
        }

        protected override void OnMouseMove(MouseEventArgs eventArgs)
        {
            base.OnMouseMove(eventArgs);
            if (_dragging)
            {
                SetValueFromX(eventArgs.X);
            }
        }

        protected override void OnMouseUp(MouseEventArgs eventArgs)
        {
            base.OnMouseUp(eventArgs);
            _dragging = false;
            Capture = false;
        }

        private void SetValueFromX(int x)
        {
            var trackLeft = 10;
            var trackRight = Width - 10;
            var ratio = trackRight == trackLeft ? 0 : (double)(x - trackLeft) / (trackRight - trackLeft);
            Value = _minimum + (int)Math.Round(Math.Min(Math.Max(ratio, 0), 1) * (_maximum - _minimum));
        }
    }

    private sealed class DarkNumberInput : UserControl
    {
        private readonly TextBox _textBox = new();
        private readonly Button _upButton = new();
        private readonly Button _downButton = new();
        private decimal _minimum;
        private decimal _maximum = 100;
        private decimal _value;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public decimal Minimum
        {
            get => _minimum;
            set
            {
                _minimum = value;
                if (_maximum < _minimum)
                {
                    _maximum = _minimum;
                }

                Value = _value;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public decimal Maximum
        {
            get => _maximum;
            set
            {
                _maximum = Math.Max(value, _minimum);
                Value = _value;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public decimal Value
        {
            get => _value;
            set
            {
                _value = Math.Min(Math.Max(value, _minimum), _maximum);
                _textBox.Text = _value.ToString("0");
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public decimal Increment { get; set; } = 1;

        public DarkNumberInput()
        {
            Height = 34;
            Dock = DockStyle.Top;
            BackColor = PanelBackgroundAlt;
            Padding = new Padding(1);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = PanelBackgroundAlt
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            Controls.Add(layout);

            _textBox.BorderStyle = BorderStyle.None;
            _textBox.BackColor = PanelBackgroundAlt;
            _textBox.ForeColor = TextPrimary;
            _textBox.Dock = DockStyle.Fill;
            _textBox.TextAlign = HorizontalAlignment.Center;
            _textBox.Margin = new Padding(8, 8, 8, 0);
            _textBox.Leave += (_, _) => CommitText();
            _textBox.KeyDown += (_, eventArgs) =>
            {
                if (eventArgs.KeyCode == Keys.Enter)
                {
                    CommitText();
                    eventArgs.SuppressKeyPress = true;
                }
            };
            layout.Controls.Add(_textBox, 0, 0);

            var buttons = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0)
            };
            buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            layout.Controls.Add(buttons, 1, 0);

            ConfigureStepperButton(_upButton, "+");
            ConfigureStepperButton(_downButton, "-");
            _upButton.Click += (_, _) => Value += Increment;
            _downButton.Click += (_, _) => Value -= Increment;
            buttons.Controls.Add(_upButton, 0, 0);
            buttons.Controls.Add(_downButton, 0, 1);
        }

        protected override void OnEnabledChanged(EventArgs eventArgs)
        {
            base.OnEnabledChanged(eventArgs);
            _textBox.Enabled = Enabled;
            _upButton.Enabled = Enabled;
            _downButton.Enabled = Enabled;
        }

        private static void ConfigureStepperButton(Button button, string text)
        {
            button.Text = text;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0);
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Color.FromArgb(42, 42, 47);
            button.ForeColor = TextPrimary;
            button.FlatAppearance.BorderColor = BorderColor;
            button.FlatAppearance.BorderSize = 1;
        }

        private void CommitText()
        {
            if (decimal.TryParse(_textBox.Text, out var parsed))
            {
                Value = parsed;
                return;
            }

            _textBox.Text = _value.ToString("0");
        }
    }

    /// <summary>Game-facing members are null in voice test mode, where nothing is attached.</summary>
    private sealed class RuntimeContext(
        ProcessMemoryReader? processReader,
        MenuStateReader? menuReader,
        MenuStateReader? teamSelectionReader,
        KeyboardInput? keyboardInput,
        VoiceCommandParser parser,
        CommandPlanExecutor? executor,
        VoiceReadySettings settings) : IDisposable
    {
        public ProcessMemoryReader? ProcessReader { get; } = processReader;

        public MenuStateReader? MenuReader { get; } = menuReader;

        public MenuStateReader? TeamSelectionReader { get; } = teamSelectionReader;

        public KeyboardInput? KeyboardInput { get; } = keyboardInput;

        public VoiceCommandParser Parser { get; } = parser;

        public CommandPlanExecutor? Executor { get; } = executor;

        public VoiceReadySettings Settings { get; } = settings;

        public Task? VoiceTask { get; set; }

        public void Dispose()
        {
            ProcessReader?.Dispose();
        }
    }
}
