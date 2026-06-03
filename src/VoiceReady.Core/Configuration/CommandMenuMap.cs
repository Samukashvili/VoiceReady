namespace VoiceReady.Core.Configuration;

public sealed class CommandMenuMap
{
    public string DefaultStackUpMode { get; init; } = "Auto";

    public List<CommandMenuState> States { get; init; } = [];
}

public sealed class CommandMenuState
{
    public string Name { get; init; } = string.Empty;

    public int MemoryValue { get; init; }

    public string Category { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public List<CommandMenuOption> Commands { get; init; } = [];

    public List<CommandMenuVariant> Variants { get; init; } = [];
}

public sealed class CommandMenuOption
{
    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string OpensState { get; init; } = string.Empty;

    public string DefaultFor { get; init; } = string.Empty;

    public string VariantNotes { get; init; } = string.Empty;
}

public sealed class CommandMenuVariant
{
    public string Name { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;
}
