namespace VoiceReady.Core.Transcription;

public static class VoiceCommandGrammar
{
    private static readonly string[] TeamPrefixes =
    [
        "", "red team", "blue team", "gold team", "all teams", "everyone"
    ];

    private static readonly string[] BreachMethods =
    [
        "kick", "kick the door", "shotgun", "use the shotgun", "c two", "c four",
        "use c two", "use c four", "breaching charge", "explosives", "use explosives",
        "ram", "use the ram", "leader"
    ];

    private static readonly string[] ClearMethods =
    [
        "clear", "clear the room", "clear with flash bang", "clear with a flash bang",
        "clear with stinger", "clear with a stinger", "clear with c s gas",
        "clear with gas", "clear with nine bang", "clear with launcher",
        "clear with the launcher", "clear with leader"
    ];

    private static readonly string[] StandaloneCommands =
    [
        "open the door", "open door", "close the door", "close door",
        "mirror under the door", "mirror the door", "wedge the door", "jam the door",
        "block the door", "remove the wedge", "remove the door wedge", "remove the door jam",
        "unblock the door", "remove the jam", "stack up", "stack up left", "stack up right",
        "stack up split", "stack up auto", "scan", "scan the room", "slide", "pie", "peek",
        "move", "move here", "move there", "move to", "here", "my position", "stop moving",
        "cover", "hold", "hold position",
        "fall in", "falling", "falling in", "all in", "on me", "get behind me",
        "single file", "double file", "diamond formation", "wedge formation",
        "deploy flash bang", "deploy stinger", "deploy c s gas", "deploy gas",
        "deploy nine bang", "deploy chem light", "deploy shield", "search area",
        "search the area", "search and secure", "secure the area", "search room",
        "restrain", "restrain them", "arrest", "arrest them", "cuff them",
        "turn around", "face away", "move to exit", "move to my position", "stop",
        "focus here", "focus on my position", "focus on the door", "focus on target",
        "stop focus", "swap with alpha", "swap with bravo", "swap with charlie", "swap with delta",
        "red team", "blue team", "gold team", "all teams", "everyone"
    ];

    public static IReadOnlyList<string> Build(IEnumerable<string>? additionalPhrases = null, bool includeUnknownWords = true)
    {
        var phrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var team in TeamPrefixes)
        {
            foreach (var command in StandaloneCommands)
            {
                AddWithTeam(phrases, team, command);
            }

            foreach (var breachMethod in BreachMethods)
            {
                AddWithTeam(phrases, team, $"breach with {breachMethod}");
                AddWithTeam(phrases, team, $"breach using {breachMethod}");
                AddWithTeam(phrases, team, breachMethod);

                foreach (var clearMethod in ClearMethods)
                {
                    AddWithTeam(phrases, team, $"breach with {breachMethod} and {clearMethod}");
                    AddWithTeam(phrases, team, $"breach using {breachMethod} and {clearMethod}");
                    AddWithTeam(phrases, team, $"{breachMethod} and {clearMethod}");
                }
            }

            foreach (var clearMethod in ClearMethods)
            {
                AddWithTeam(phrases, team, clearMethod.Replace("clear", "move in"));
                AddWithTeam(phrases, team, clearMethod.Replace("clear", "move"));
                AddWithTeam(phrases, team, clearMethod.Replace("clear with", "move in using"));
                AddWithTeam(phrases, team, clearMethod.Replace("clear with", "move using"));
            }
        }

        foreach (var phrase in additionalPhrases ?? [])
        {
            if (!string.IsNullOrWhiteSpace(phrase))
            {
                phrases.Add(phrase.Trim().ToLowerInvariant());
            }
        }

        if (includeUnknownWords)
        {
            phrases.Add("[unk]");
        }

        return phrases.OrderBy(phrase => phrase, StringComparer.Ordinal).ToArray();
    }

    private static void AddWithTeam(HashSet<string> phrases, string team, string command)
    {
        phrases.Add(string.IsNullOrWhiteSpace(team) ? command : $"{team} {command}");
    }
}
