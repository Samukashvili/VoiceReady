using System.Text.Json.Serialization;
using VoiceReady.Core.Memory;

namespace VoiceReady.Core.Configuration;

public sealed class MemoryMap
{
    public string[] ProcessNames { get; init; } = [];

    public PointerOffsetOrder OffsetOrder { get; init; } = PointerOffsetOrder.CheatEnginePointerScanner;

    public MenuStateMap MenuState { get; init; } = new();
}

public sealed class MenuStateMap
{
    public string ModuleName { get; init; } = "ReadyOrNotSteam-Win64-Shipping.exe";

    public PointerOffsetOrder OffsetOrder { get; init; } = PointerOffsetOrder.CheatEnginePointerScanner;

    public List<MenuPointerMap> Pointers { get; init; } = [];

    public List<KnownMenuState> KnownStates { get; init; } = [];

    [JsonIgnore]
    public IReadOnlyList<PointerPath> PointerPaths => Pointers
        .Select((pointer, index) => PointerPath.FromHex(
            string.IsNullOrWhiteSpace(pointer.Name) ? $"menuState.{index + 1}" : pointer.Name,
            string.IsNullOrWhiteSpace(pointer.ModuleName) ? ModuleName : pointer.ModuleName,
            pointer.BaseOffset,
            pointer.Offsets,
            pointer.OffsetOrder ?? OffsetOrder))
        .ToArray();
}

public sealed class MenuPointerMap
{
    public string Name { get; init; } = string.Empty;

    public string ModuleName { get; init; } = string.Empty;

    public string BaseOffset { get; init; } = string.Empty;

    public List<string> Offsets { get; init; } = [];

    public PointerOffsetOrder? OffsetOrder { get; init; }
}

public sealed class KnownMenuState
{
    public string Name { get; init; } = string.Empty;

    public int Value { get; init; }
}
