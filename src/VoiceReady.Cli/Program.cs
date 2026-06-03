using VoiceReady.Core.Configuration;
using VoiceReady.Core.Detection;
using VoiceReady.Core.Input;
using VoiceReady.Core.Memory;

var memoryMapPath = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "config", "memory_map.json"));
var commandMenuMapPath = Path.Combine(Path.GetDirectoryName(memoryMapPath) ?? AppContext.BaseDirectory, "command_menus.json");

if (!File.Exists(memoryMapPath))
{
    Console.Error.WriteLine($"Memory map not found: {memoryMapPath}");
    return 1;
}

MemoryMap memoryMap;
CommandMenuMap? commandMenuMap = null;
try
{
    memoryMap = MemoryMapLoader.Load(memoryMapPath);
    if (File.Exists(commandMenuMapPath))
    {
        commandMenuMap = CommandMenuMapLoader.Load(commandMenuMapPath);
    }
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
var knownStates = memoryMap.MenuState.KnownStates.ToDictionary(state => state.Value, state => state.Name);
var commandStates = commandMenuMap?.States.ToDictionary(state => state.MemoryValue) ?? [];
var semicolonKey = new SemicolonKeyPoller();
var temporaryExecutor = new TemporaryDoorCommandExecutor(menuReader, new KeyboardInput());

Console.WriteLine($"Attached to {processReader.ProcessName} ({processReader.ProcessId}).");
Console.WriteLine($"Reading {memoryMap.MenuState.PointerPaths.Count} menu-state pointer paths.");
Console.WriteLine("Temporary test hotkey: ; executes Door -> Breach -> C2 -> Clear when DoorCommandMenu is active.");
Console.WriteLine("Press Ctrl+C to stop.");

int? lastValue = null;
var hasPrinted = false;

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
        var stateName = snapshot.VotedValue.HasValue && knownStates.TryGetValue(snapshot.VotedValue.Value, out var knownState)
            ? knownState
            : "Unmapped";

        if (!hasPrinted || snapshot.VotedValue != lastValue)
        {
            var valueText = snapshot.VotedValue.HasValue
                ? $"{snapshot.VotedValue.Value} ({stateName})"
                : "no consensus";
            var commandText = snapshot.VotedValue.HasValue && commandStates.TryGetValue(snapshot.VotedValue.Value, out var commandState)
                ? $", category={commandState.Category}, commands={commandState.Commands.Count}"
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

        Thread.Sleep(15);
    }
}

return 0;
