using System.Text.RegularExpressions;

namespace VoiceReady.Core.Commands;

public sealed class VoiceCommandParser
{
    private static readonly Regex NonWords = new("[^a-z0-9 ]+", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new("\\s+", RegexOptions.Compiled);

    private static readonly string[] FillerWords =
    [
        "the", "a", "an", "and", "then", "using", "use", "with", "to", "in", "on", "at",
        "please", "team", "guys", "officers", "fucking", "fuckin", "damn", "goddamn", "him", "her", "it"
    ];

    public CommandPlan? Parse(string text)
    {
        var normalized = Normalize(text);
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (HasAny(tokens, "breach", "reach", "bridge", "breech", "break", "broke") ||
            HasAny(tokens, "kick", "kicking") && HasAny(tokens, "door", "clear"))
        {
            return ParseDoorBreach(tokens);
        }

        if (HasAny(tokens, "search", "secure") && HasAny(tokens, "area", "room", "secure"))
        {
            return new CommandPlan(
                "GroundSearchArea",
                "GroundCommandMenu",
                [new CommandStep("6", "SearchArea")]);
        }

        if (HasAny(tokens, "restrain", "restraining", "restraying", "arrest", "aristing", "cuff", "cuffs", "restoring"))
        {
            return new CommandPlan(
                "CivilianSuspectRestrain",
                "CivilianSuspectCommandMenu",
                [new CommandStep("1", "Restrain")]);
        }

        if ((HasAny(tokens, "turn") && HasAny(tokens, "around", "road", "round")) || HasPhrase(normalized, "face away"))
        {
            return new CommandPlan(
                "CivilianSuspectTurnAround",
                "CivilianSuspectCommandMenu",
                [new CommandStep("4", "TurnAround")]);
        }

        if (HasAny(tokens, "mirror") || HasPhrase(normalized, "under door"))
        {
            return new CommandPlan(
                "DoorMirrorUnderDoor",
                "DoorCommandMenu",
                [new CommandStep("5", "MirrorUnderDoor")]);
        }

        if (HasAny(tokens, "clear") || HasPhrase(normalized, "go in") || HasPhrase(normalized, "going in"))
        {
            return ParseClear(tokens);
        }

        if (HasPhrase(normalized, "stack up") || HasAny(tokens, "stack"))
        {
            var stackKey = HasAny(tokens, "left") ? "2"
                : HasAny(tokens, "right") ? "3"
                : HasAny(tokens, "split") ? "1"
                : "4";

            return new CommandPlan(
                "DoorStackUp",
                "DoorCommandMenu",
                [
                    new CommandStep("1", "StackUp", "DoorStackUpSubmenu"),
                    new CommandStep(stackKey, "StackMode")
                ],
                AlternativeInitialStates: ["DoorwayCommandMenu"]);
        }

        if (HasPhrase(normalized, "move to") || HasAny(tokens, "move", "go") && HasAny(tokens, "there", "here"))
        {
            return new CommandPlan(
                "GroundMoveTo",
                "GroundCommandMenu",
                [new CommandStep("1", "MoveTo")]);
        }

        if (HasPhrase(normalized, "fall in") || HasAny(tokens, "fall", "folding", "formation", "wedge", "diamond"))
        {
            var formationKey = HasAny(tokens, "single") ? "1"
                : HasAny(tokens, "double") ? "2"
                : HasAny(tokens, "diamond") ? "3"
                : "4";

            return new CommandPlan(
                "GroundFallIn",
                "GroundCommandMenu",
                [
                    new CommandStep("2", "FallIn", "GroundFallInSubmenu"),
                    new CommandStep(formationKey, "Formation")
                ]);
        }

        return null;
    }

    private static CommandPlan ParseClear(string[] tokens)
    {
        var clearKey = GetClearKey(tokens);

        return new CommandPlan(
            "DoorOrDoorwayClear",
            "DoorCommandMenu",
            [
                new CommandStep("2", "OpenOrMove", "DoorOpenSubmenu"),
                new CommandStep(clearKey, "ClearMethod")
            ],
            AlternativeInitialStates: ["DoorwayCommandMenu"]);
    }

    private static CommandPlan ParseDoorBreach(string[] tokens)
    {
        var breachKey = HasAny(tokens, "shotgun", "shotty") ? "2"
            : HasAny(tokens, "c2", "c4", "charge", "charges", "explosive", "boom") ? "3"
            : HasAny(tokens, "ram", "battering") ? "4"
            : HasAny(tokens, "leader", "lead") ? "5"
            : "1";

        var clearKey = GetClearKey(tokens);

        return new CommandPlan(
            "DoorBreach",
            "DoorCommandMenu",
            [
                new CommandStep("3", "Breach", "DoorBreachSubmenu"),
                new CommandStep(breachKey, "BreachMethod", "DoorOpenSubmenu"),
                new CommandStep(clearKey, "ClearMethod")
            ]);
    }

    private static string GetClearKey(string[] tokens)
    {
        return HasAny(tokens, "flash", "flashbang") ? "2"
            : HasAny(tokens, "stinger", "sting") ? "3"
            : HasAny(tokens, "cs", "gas", "tear") ? "4"
            : HasAny(tokens, "9bang", "ninebang", "nine", "bang") ? "5"
            : HasAny(tokens, "launcher", "launch", "grenade") ? "6"
            : HasAny(tokens, "leader", "lead") ? "7"
            : "1";
    }

    private static bool HasAny(IEnumerable<string> tokens, params string[] options)
    {
        var tokenSet = tokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return options.Any(tokenSet.Contains);
    }

    private static bool HasPhrase(string normalized, string phrase)
    {
        return normalized.Contains(phrase, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string text)
    {
        var normalized = text.Trim().ToLowerInvariant()
            .Replace("c two", "c2")
            .Replace("c too", "c2")
            .Replace("c to", "c2")
            .Replace("c-2", "c2")
            .Replace("c four", "c4")
            .Replace("c for", "c4")
            .Replace("9-bang", "9bang")
            .Replace("nine bang", "ninebang");

        normalized = NonWords.Replace(normalized, " ");
        var tokens = Whitespace.Split(normalized)
            .Where(token => token.Length > 0)
            .Where(token => !FillerWords.Contains(token, StringComparer.OrdinalIgnoreCase));

        return string.Join(' ', tokens);
    }
}
