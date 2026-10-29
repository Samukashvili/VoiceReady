namespace VoiceReady.Core.Commands;

/// <param name="CompletesIfMenuCloses">
/// The game sometimes runs this entry directly instead of opening <paramref name="ExpectedStateAfter"/>
/// (e.g. Stack Up on a door that only has one stack side). When the menu closes, the plan is done.
/// </param>
public sealed record CommandStep(string Key, string Name, string? ExpectedStateAfter = null, bool CompletesIfMenuCloses = false);
