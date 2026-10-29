using System.Text.Json.Serialization;
using VoiceReady.Core.Memory;
using VoiceReady.Core.Memory.Unreal;

namespace VoiceReady.Core.Configuration;

public sealed class MemoryMap
{
    public string[] ProcessNames { get; init; } = [];

    /// <summary>Wildcard used to auto-detect other Ready or Not builds (e.g. non-Steam stores).</summary>
    public string ProcessNamePattern { get; init; } = "ReadyOrNot*-Win64-Shipping";

    public PointerOffsetOrder OffsetOrder { get; init; } = PointerOffsetOrder.CheatEnginePointerScanner;

    public MenuStateMap MenuState { get; init; } = new();

    public TeamSelectionMap TeamSelection { get; init; } = new();

    public ReflectionMap Reflection { get; init; } = new();
}

public sealed class ReflectionMap
{
    public bool Enabled { get; init; } = true;

    public List<SignatureMap> NamePoolSignatures { get; init; } = [];

    public List<SignatureMap> GWorldSignatures { get; init; } = [];

    public UnrealLayout Layout { get; init; } = new();

    public string MenuOpenPath { get; init; } = string.Empty;

    public string EscapeMenuOpenPath { get; init; } = string.Empty;

    public string CommandPagePath { get; init; } = string.Empty;

    public string ParentCommandsPath { get; init; } = string.Empty;

    public string CommandTypeField { get; init; } = "CommandType";

    public string TeamSelectionPath { get; init; } = string.Empty;
}

public sealed class SignatureMap
{
    public string Pattern { get; init; } = string.Empty;

    public int DisplacementOffset { get; init; }

    public int InstructionEndOffset { get; init; }

    public PointerRootSignature ToSignature() => new(Pattern, DisplacementOffset, InstructionEndOffset);
}

public abstract class PointerGroupMap
{
    public string ModuleName { get; init; } = "ReadyOrNotSteam-Win64-Shipping.exe";

    public PointerOffsetOrder OffsetOrder { get; init; } = PointerOffsetOrder.CheatEnginePointerScanner;

    public List<MenuPointerMap> Pointers { get; init; } = [];

    public List<PointerRootSignatureMap> RootSignatures { get; init; } = [];

    protected IReadOnlyList<PointerPath> BuildPointerPaths(string prefix)
    {
        return Pointers
            .Select((pointer, index) => PointerPath.FromHex(
                string.IsNullOrWhiteSpace(pointer.Name) ? $"{prefix}.{index + 1}" : pointer.Name,
                string.IsNullOrWhiteSpace(pointer.ModuleName) ? ModuleName : pointer.ModuleName,
                pointer.BaseOffset,
                pointer.Offsets,
                FindRootSignature(pointer),
                pointer.OffsetOrder ?? OffsetOrder))
            .ToArray();
    }

    private PointerRootSignature? FindRootSignature(MenuPointerMap pointer)
    {
        var moduleName = string.IsNullOrWhiteSpace(pointer.ModuleName) ? ModuleName : pointer.ModuleName;
        return RootSignatures.FirstOrDefault(root =>
            string.Equals(root.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(root.BaseOffset, pointer.BaseOffset, StringComparison.OrdinalIgnoreCase))?.ToSignature();
    }
}

public sealed class MenuStateMap : PointerGroupMap
{
    public List<KnownMenuState> KnownStates { get; init; } = [];

    [JsonIgnore]
    public IReadOnlyList<PointerPath> PointerPaths => BuildPointerPaths("menuState");
}

public sealed class TeamSelectionMap : PointerGroupMap
{
    public List<KnownTeamSelection> KnownSelections { get; init; } = [];

    [JsonIgnore]
    public IReadOnlyList<PointerPath> PointerPaths => BuildPointerPaths("teamSelection");
}

public sealed class MenuPointerMap
{
    public string Name { get; init; } = string.Empty;

    public string ModuleName { get; init; } = string.Empty;

    public string BaseOffset { get; init; } = string.Empty;

    public List<string> Offsets { get; init; } = [];

    public PointerOffsetOrder? OffsetOrder { get; init; }
}

public sealed class PointerRootSignatureMap
{
    public string ModuleName { get; init; } = "ReadyOrNotSteam-Win64-Shipping.exe";

    public string BaseOffset { get; init; } = string.Empty;

    public string Pattern { get; init; } = string.Empty;

    public int DisplacementOffset { get; init; }

    public int InstructionEndOffset { get; init; }

    public PointerRootSignature ToSignature() => new(Pattern, DisplacementOffset, InstructionEndOffset);
}

public sealed class KnownMenuState
{
    public string Name { get; init; } = string.Empty;

    public int Value { get; init; }

    public List<string> Aliases { get; init; } = [];

    /// <summary>
    /// Command pages (ESwatCommand names, without the SC_ prefix) that identify this state when read through
    /// reflection. "None" is a submenu entry, "*" is a wildcard inside a name, and "|" separates alternatives.
    /// </summary>
    public List<List<string>> CommandPages { get; init; } = [];

    /// <summary>When set, the page only matches at the root menu (false) or inside a submenu (true).</summary>
    public bool? Submenu { get; init; }
}

public sealed class KnownTeamSelection
{
    public string Name { get; init; } = string.Empty;

    public int Value { get; init; }

    public List<string> Aliases { get; init; } = [];
}
