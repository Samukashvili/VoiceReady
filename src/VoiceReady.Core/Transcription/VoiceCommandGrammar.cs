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
        // Door
        "open the door", "open door", "open it", "close the door", "close door", "close it",
        "shut the door", "shut it",
        "mirror under the door", "mirror the door", "mirror it", "use the mirror", "mirror",
        "check under the door", "look under the door",
        "wedge the door", "wedge it", "wedge", "jam the door", "jam it", "block the door", "block it",
        "remove the wedge", "remove wedge", "remove the door wedge", "remove the door jam",
        "unblock the door", "remove the jam", "pull the wedge", "take the wedge",
        "disarm the trap", "disarm trap", "disarm it", "disarm", "disarm the door", "trap",
        "defuse the trap", "defuse it", "defuse", "disable the trap", "remove the trap", "remove trap",
        "cut the wire",
        "pick the lock", "pick lock", "pick it", "lock pick the door",
        "stack up", "stack up left", "stack up right", "stack up split", "stack up auto",
        "stack left", "stack right", "stack split", "stack on the door", "stack up on the door",
        "line up", "line up on the door",
        "scan", "scan the room", "scan the door", "scan it",
        "slide", "slide scan", "pie", "pie the room", "pie it", "peek", "peek the door", "quick peek",
        // Ground
        "move", "move here", "move there", "move to", "move up", "go there", "go here",
        "move there and come back", "go there and come back", "move and come back",
        "here", "my position", "come here", "come to me", "stop moving",
        "cover", "cover me", "cover that", "cover there", "cover the area", "watch that", "watch there",
        "hold", "hold position", "hold here", "hold up", "stay here", "wait here", "stay", "wait",
        // "falling" is deliberately absent: with it in the vocabulary, "fall in" is often heard as "falling".
        "fall in", "all in", "on me", "get behind me", "fall in on me",
        "form up", "regroup", "follow me",
        "single file", "double file", "diamond formation", "wedge formation",
        "fall in single file", "fall in double file", "fall in diamond", "fall in wedge",
        "deploy flash bang", "deploy stinger", "deploy c s gas", "deploy gas",
        "deploy nine bang", "deploy chem light", "deploy shield",
        "throw a flash bang", "throw flash bang", "throw a stinger", "throw stinger",
        "throw gas", "throw a chem light", "drop a chem light", "toss a flash bang", "toss a stinger",
        "search area", "search the area", "search and secure", "secure the area", "search room",
        "search the room", "search", "sweep the area", "sweep the room",
        // Context items: clear with it at a door, deploy it on the ground.
        "use the flash bang", "use a flash bang", "use flash bang", "flash bang",
        "use the stinger", "use a stinger", "use stinger", "stinger",
        "use the gas", "use gas", "use c s gas", "gas",
        "use the nine bang", "use a nine bang", "nine bang",
        "use a chem light", "use the shield",
        // Civilians and suspects
        "restrain", "restrain them", "restrain him", "restrain her", "arrest", "arrest them",
        "arrest him", "cuff them", "cuff him", "cuff her", "zip tie them", "detain them",
        "turn around", "face away", "face the wall",
        "move to exit", "move to the exit", "escort to exit", "escort them out",
        "move to my position", "stop",
        // Individual officers
        "focus here", "focus there", "focus on my position", "focus on me", "focus on the door",
        "focus door", "focus on target", "focus on the target", "focus on the suspect",
        "stop focus", "stop focusing",
        "swap with alpha", "swap with bravo", "swap with charlie", "swap with delta",
        "switch with alpha", "switch with bravo", "switch with charlie", "switch with delta",
        "trade places with alpha", "trade places with bravo", "trade places with charlie",
        "trade places with delta",
        // Teams
        "red team", "blue team", "gold team", "all teams", "everyone"
    ];

    private static readonly string[] StandaloneBreaches =
    [
        "breach it", "breach the door", "breach", "kick it", "kick it in", "kick the door in",
        "blow it", "blow the door", "blow it up", "ram it", "ram the door", "shotgun it",
        "shotgun the door"
    ];

    /// <summary>Leader clear: the team waits for the player to enter first.</summary>
    private static readonly string[] LeaderPhrases =
    [
        "on my go", "on my lead", "on my command", "and follow my lead", "and follow me in", "and wait for me"
    ];

    private static readonly string[] ClearItems =
    [
        "flash bang", "flash", "stinger", "c s gas", "gas", "nine bang", "launcher", "leader"
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

            foreach (var breach in StandaloneBreaches)
            {
                AddWithTeam(phrases, team, breach);
                AddWithTeam(phrases, team, $"{breach} and clear");
            }

            foreach (var leaderPhrase in LeaderPhrases)
            {
                AddWithTeam(phrases, team, $"move {leaderPhrase}");
                AddWithTeam(phrases, team, $"move in {leaderPhrase}");
                AddWithTeam(phrases, team, $"clear {leaderPhrase}");
                AddWithTeam(phrases, team, $"open and clear {leaderPhrase}");
                AddWithTeam(phrases, team, $"breach {leaderPhrase}");
                AddWithTeam(phrases, team, $"breach the door {leaderPhrase}");
                foreach (var breachMethod in BreachMethods)
                {
                    AddWithTeam(phrases, team, $"breach with {breachMethod} {leaderPhrase}");
                    AddWithTeam(phrases, team, $"{breachMethod} {leaderPhrase}");
                }
            }

            AddWithTeam(phrases, team, "open and clear");
            AddWithTeam(phrases, team, "go in");
            AddWithTeam(phrases, team, "move in and clear");
            AddWithTeam(phrases, team, "enter and clear");
            foreach (var item in ClearItems)
            {
                AddWithTeam(phrases, team, $"{item} and clear");
                AddWithTeam(phrases, team, $"open and clear with {item}");
                AddWithTeam(phrases, team, $"open and clear with a {item}");
                AddWithTeam(phrases, team, $"clear with {item}");
                AddWithTeam(phrases, team, $"move in with {item}");
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
