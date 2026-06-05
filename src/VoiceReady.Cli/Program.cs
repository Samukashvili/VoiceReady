using VoiceReady.Core.Audio;
using VoiceReady.Core.Commands;
using VoiceReady.Core.Configuration;
using VoiceReady.Core.Detection;
using VoiceReady.Core.Input;
using VoiceReady.Core.Memory;
using VoiceReady.Core.Transcription;

var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
var memoryMapPath = args.Length > 0 && args[0].EndsWith(".json", StringComparison.OrdinalIgnoreCase)
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "config", "memory_map.json"));
var configDir = Path.GetDirectoryName(memoryMapPath) ?? AppContext.BaseDirectory;
var commandMenuMapPath = Path.Combine(configDir, "command_menus.json");
var voiceSettingsPath = Path.Combine(configDir, "voice_ready.json");
var voiceEnabled = args.Any(arg => arg.Equals("--voice", StringComparison.OrdinalIgnoreCase));

if (!File.Exists(memoryMapPath))
{
    Console.Error.WriteLine($"Memory map not found: {memoryMapPath}");
    return 1;
}

MemoryMap memoryMap;
CommandMenuMap? commandMenuMap = null;
VoiceReadySettings voiceSettings;
try
{
    memoryMap = MemoryMapLoader.Load(memoryMapPath);
    if (File.Exists(commandMenuMapPath))
    {
        commandMenuMap = CommandMenuMapLoader.Load(commandMenuMapPath);
    }

    voiceSettings = File.Exists(voiceSettingsPath)
        ? VoiceReadySettingsLoader.Load(voiceSettingsPath)
        : new VoiceReadySettings();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Could not load config: {ex.Message}");
    return 1;
}

ProcessMemoryReader processReader;
try
{
    processReader = ProcessMemoryReader.AttachByProcessName(memoryMap.ProcessNames);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Could not attach to Ready or Not: {ex.Message}");
    return 1;
}

using (processReader)
{
    var shutdown = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        shutdown.Cancel();
    };

    var menuReader = new MenuStateReader(processReader, memoryMap.MenuState.PointerPaths);
    var teamSelectionReader = new MenuStateReader(processReader, memoryMap.TeamSelection.PointerPaths);
    var knownStates = memoryMap.MenuState.KnownStates.ToDictionary(
        state => state.Value,
        state => state.Aliases.Count == 0 ? state.Name : $"{state.Name}/{string.Join("/", state.Aliases)}");
    var commandStates = commandMenuMap?.States
        .GroupBy(state => state.MemoryValue)
        .ToDictionary(group => group.Key, group => group.ToArray())
        ?? [];
    var keyboardInput = new KeyboardInput();
    var semicolonKey = new SemicolonKeyPoller();
    var temporaryExecutor = new TemporaryDoorCommandExecutor(menuReader, keyboardInput);
    var parser = new VoiceCommandParser();
    var planExecutor = new CommandPlanExecutor(
        menuReader,
        memoryMap.MenuState.KnownStates,
        teamSelectionReader,
        memoryMap.TeamSelection.KnownSelections,
        keyboardInput,
        voiceSettings.Input);

    Console.WriteLine($"Attached to {processReader.ProcessName} ({processReader.ProcessId}).");
    Console.WriteLine($"Reading {memoryMap.MenuState.PointerPaths.Count} menu-state pointer paths.");
    Console.WriteLine($"Reading {memoryMap.TeamSelection.PointerPaths.Count} team-selection pointer paths.");
    Console.WriteLine("Temporary test hotkey: ; executes Door -> Breach -> C2 -> Clear when DoorCommandMenu is active.");
    Console.WriteLine(voiceEnabled ? "Voice mode enabled." : "Voice mode disabled. Start with --voice to enable microphone transcription.");
    Console.WriteLine("Press Ctrl+C to stop.");

    Task? voiceTask = null;
    if (voiceEnabled)
    {
        voiceTask = RunVoiceLoopAsync(repoRoot, voiceSettings, parser, planExecutor, shutdown.Token);
    }

    int? lastValue = null;
    int? lastTeamValue = null;
    var hasPrinted = false;
    var hasPrintedRootResolutions = false;

    while (!shutdown.IsCancellationRequested)
    {
        if (semicolonKey.WasPressed())
        {
            try
            {
                var executed = temporaryExecutor.TryExecuteBreachC2Clear(out var message);
                Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} hotkey=; result={(executed ? "sent" : "blocked")} {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} hotkey=; result=error {ex.Message}");
            }
        }

        var snapshot = menuReader.Read();
        var teamSnapshot = teamSelectionReader.Read();

        if (!hasPrintedRootResolutions)
        {
            foreach (var root in processReader.RootResolutions.OrderBy(root => root.ConfiguredOffset))
            {
                var source = root.UsedSignature ? "signature" : "fallback";
                var moved = root.ConfiguredOffset == root.ResolvedOffset
                    ? string.Empty
                    : $" moved-from=+0x{root.ConfiguredOffset:X}";
                var reason = string.IsNullOrWhiteSpace(root.FallbackReason)
                    ? string.Empty
                    : $" reason={root.FallbackReason}";
                Console.WriteLine(
                    $"Pointer root {root.ModuleName}+0x{root.ResolvedOffset:X} source={source}{moved}{reason}");
            }

            hasPrintedRootResolutions = true;
        }
        var stateName = snapshot.VotedValue.HasValue && knownStates.TryGetValue(snapshot.VotedValue.Value, out var knownState)
            ? knownState
            : "Unmapped";

        if (!hasPrinted || snapshot.VotedValue != lastValue)
        {
            var valueText = snapshot.VotedValue.HasValue
                ? $"{snapshot.VotedValue.Value} ({stateName})"
                : "no consensus";
            var commandText = snapshot.VotedValue.HasValue && commandStates.TryGetValue(snapshot.VotedValue.Value, out var matchingCommandStates)
                ? $", states={string.Join("/", matchingCommandStates.Select(state => state.Name))}, commands={string.Join("/", matchingCommandStates.Select(state => state.Commands.Count))}"
                : string.Empty;

            Console.WriteLine(
                $"{DateTimeOffset.Now:HH:mm:ss.fff} value={valueText}, confidence={snapshot.Confidence:P0}, reads={snapshot.SuccessfulReads}, failed={snapshot.FailedReads}{commandText}");

            if (snapshot.SuccessfulReads == 0)
            {
                foreach (var failedRead in snapshot.Reads.Where(read => !read.Success).Take(5))
                {
                    Console.WriteLine($"  {failedRead.Pointer.Name}: {failedRead.Error}");
                }
            }

            lastValue = snapshot.VotedValue;
            hasPrinted = true;
        }

        if (teamSnapshot.VotedValue != lastTeamValue)
        {
            var teamName = memoryMap.TeamSelection.KnownSelections
                .FirstOrDefault(selection => selection.Value == teamSnapshot.VotedValue)?.Name
                ?? "Unmapped";
            Console.WriteLine(
                $"{DateTimeOffset.Now:HH:mm:ss.fff} team={teamSnapshot.VotedValue?.ToString() ?? "no consensus"} ({teamName}), confidence={teamSnapshot.Confidence:P0}, reads={teamSnapshot.SuccessfulReads}, failed={teamSnapshot.FailedReads}");
            lastTeamValue = teamSnapshot.VotedValue;
        }

        Thread.Sleep(15);
    }

    if (voiceTask is not null)
    {
        await voiceTask.WaitAsync(TimeSpan.FromSeconds(2));
    }
}

return 0;

static async Task RunVoiceLoopAsync(
    string repoRoot,
    VoiceReadySettings settings,
    VoiceCommandParser parser,
    CommandPlanExecutor executor,
    CancellationToken cancellationToken)
{
    using var transcriber = new VoskTranscriber(settings.Vosk, repoRoot, settings.Audio.SampleRate);
    using var audioSource = new WaveInAudioSource(settings.Audio);
    var segmenter = new SpeechSegmenter(settings.Audio);

    audioSource.Start();
    Console.WriteLine("Microphone capture and local Vosk recognition started.");

    while (!cancellationToken.IsCancellationRequested)
    {
        while (audioSource.TryRead(out var frame) && frame is not null)
        {
            var segment = segmenter.Process(frame);
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

                Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} speech=\"{result.Text}\"");
                var plan = parser.Parse(result.Text);
                if (plan is null)
                {
                    Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} voice result=ignored No command matched.");
                    continue;
                }

                var executed = executor.TryExecute(plan, out var message);
                Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} voice result={(executed ? "sent" : "blocked")} {message}");
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} voice result=error {ex.Message}");
            }
        }

        await Task.Delay(10, cancellationToken);
    }
}

static string FindRepoRoot(string startDirectory)
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
