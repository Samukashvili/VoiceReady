namespace VoiceReady.Core.Commands;

public sealed record CommandStep(string Key, string Name, string? ExpectedStateAfter = null);
