namespace VoiceReady.Core.Commands;

public sealed record CommandPlan(
    string Name,
    string RequiredInitialState,
    IReadOnlyList<CommandStep> Steps,
    bool CanOpenMenuFromClosed = true,
    IReadOnlyList<string>? AlternativeInitialStates = null,
    string? TeamSelection = null);
