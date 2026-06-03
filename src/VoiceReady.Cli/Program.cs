using VoiceReady.Core.Configuration;
using VoiceReady.Core.Detection;
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
var menuReader = new MenuStateReader(processReader, memoryMap.MenuState.PointerPaths);
var knownStates = memoryMap.MenuState.KnownStates.ToDictionary(state => state.Value, state => state.Name);
var commandStates = commandMenuMap?.States.ToDictionary(state => state.MemoryValue) ?? [];

Console.WriteLine($"Attached to {processReader.ProcessName} ({processReader.ProcessId}).");
Console.WriteLine($"Reading {memoryMap.MenuState.PointerPaths.Count} menu-state pointer paths.");
Console.WriteLine("Press Ctrl+C to stop.");

int? lastValue = null;
var hasPrinted = false;

    while (true)
    {
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

        Thread.Sleep(50);
    }
}
