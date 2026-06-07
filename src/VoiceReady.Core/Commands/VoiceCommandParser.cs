using System.Text.RegularExpressions;

namespace VoiceReady.Core.Commands;

public sealed class VoiceCommandParser
{
    private static readonly Regex NonWords = new("[^a-z0-9 ]+", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new("\\s+", RegexOptions.Compiled);

    private static readonly string[] FillerWords =
    [
        "the", "a", "an", "and", "then", "using", "use", "with", "to", "on", "at",
        "please", "team", "guys", "officers", "fucking", "fuckin", "damn", "goddamn", "him", "her", "it"
    ];

    public CommandPlan? Parse(string text)
    {
        var normalized = Normalize(text);
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var teamSelection = ParseTeamSelection(tokens);
        var plan = ParseCommand(normalized, tokens);

        if (plan is not null)
        {
            return plan with { TeamSelection = teamSelection };
        }

        return teamSelection is null
            ? null
            : new CommandPlan($"Select{teamSelection}Team", string.Empty, [], TeamSelection: teamSelection);
    }

    private static CommandPlan? ParseCommand(string normalized, string[] tokens)
    {
        if (HasAny(tokens, "breach", "reach", "bridge", "breech", "break", "broke", "blow") ||
            HasAny(tokens, "kick", "kicking", "shotgun", "c2", "c4", "charge", "explosive", "explosives", "ram") &&
            HasAny(tokens, "door", "clear"))
        {
            return ParseDoorBreach(tokens);
        }

        if (HasAny(tokens, "remove", "unblock", "unjam") && HasAny(tokens, "wedge", "jam", "jammed", "block", "blocked", "door", "down", "time"))
        {
            return new CommandPlan(
                "DoorRemoveWedge",
                "DoorCommandMenu",
                [new CommandStep("6", "RemoveWedge")]);
        }

        if (HasAny(tokens, "wedge", "where", "jam", "jammed", "block", "blocked") && HasAny(tokens, "door"))
        {
            return new CommandPlan(
                "DoorWedge",
                "DoorCommandMenu",
                [new CommandStep("6", "WedgeDoor")]);
        }

        if (HasAny(tokens, "search", "secure") && HasAny(tokens, "area", "room", "secure"))
        {
            return new CommandPlan(
                "GroundSearchArea",
                "GroundCommandMenu",
                [new CommandStep("6", "SearchArea")]);
        }

        if (HasAny(tokens, "restrain", "restraining", "restraying", "arrest", "arresting", "aristing", "cuff", "cuffs", "restoring"))
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

        if (HasAny(tokens, "pick") && HasAny(tokens, "lock", "door"))
        {
            return new CommandPlan(
                "DoorPickLock",
                "DoorCommandMenu",
                [new CommandStep("2", "PickLock")]);
        }

        if (HasAny(tokens, "open", "close") && HasAny(tokens, "door"))
        {
            return new CommandPlan(
                HasAny(tokens, "close") ? "DoorClose" : "DoorOpen",
                "DoorCommandMenu",
                [new CommandStep("8", "OpenOrCloseDoor")]);
        }

        if (HasAny(tokens, "scan", "slide", "pie", "peek"))
        {
            var scanKey = HasAny(tokens, "slide") ? "1"
                : HasAny(tokens, "pie") ? "2"
                : HasAny(tokens, "peek") ? "3"
                : "2";

            return new CommandPlan(
                "DoorScan",
                "DoorCommandMenu",
                [
                    new CommandStep("4", "Scan", "DoorScanSubmenu"),
                    new CommandStep(scanKey, "ScanMethod")
                ],
                AlternativeInitialStates: ["DoorwayCommandMenu"]);
        }

        if (HasAny(tokens, "clear") ||
            HasPhrase(normalized, "go in") ||
            HasPhrase(normalized, "going in") ||
            HasPhrase(normalized, "move in") ||
            HasAny(tokens, "move", "moving", "enter") && HasClearMethod(tokens))
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

        if (HasAny(tokens, "deploy"))
        {
            var deployKey = HasAny(tokens, "flash", "flashbang") ? "1"
                : HasAny(tokens, "stinger", "sting") ? "2"
                : HasAny(tokens, "cs", "gas", "tear") ? "3"
                : HasAny(tokens, "9bang", "ninebang", "nine", "bang") ? "4"
                : HasAny(tokens, "chem", "chemlight", "light") ? "5"
                : HasAny(tokens, "shield") ? "6"
                : "5";

            return new CommandPlan(
                "GroundDeploy",
                "GroundCommandMenu",
                [
                    new CommandStep("5", "Deploy", "GroundDeploySubmenu"),
                    new CommandStep(deployKey, "Equipment")
                ]);
        }

        if (HasAny(tokens, "cover"))
        {
            return new CommandPlan(
                "GroundCover",
                "GroundCommandMenu",
                [new CommandStep("3", "Cover")]);
        }

        if (HasAny(tokens, "hold"))
        {
            return new CommandPlan(
                "GroundHold",
                "GroundCommandMenu",
                [new CommandStep("4", "Hold")]);
        }

        if (HasPhrase(normalized, "stop focus") || HasAny(tokens, "unfocus"))
        {
            return new CommandPlan(
                "IndividualSwatUnfocus",
                "IndividualSwatCommandMenu",
                [
                    new CommandStep("2", "Focus", "IndividualSwatFocusSubmenu"),
                    new CommandStep("5", "Unfocus")
                ]);
        }

        if (HasPhrase(normalized, "my position"))
        {
            return new CommandPlan(
                "CivilianSuspectMoveToMyPosition",
                "CivilianSuspectCommandMenu",
                [
                    new CommandStep("2", "Move", "CivilianSuspectMoveSubmenu"),
                    new CommandStep("2", "MyPosition", "GameplayNoMenu")
                ],
                StateVariants:
                [
                    new CommandPlanVariant(
                        "CivilianSuspectMoveSubmenu",
                        [new CommandStep("2", "MyPosition", "GameplayNoMenu")])
                ]);
        }

        if (HasAny(tokens, "stop") && !HasAny(tokens, "focus"))
        {
            return new CommandPlan(
                "CivilianSuspectStopMoving",
                "CivilianSuspectCommandMenu",
                [
                    new CommandStep("2", "Move", "CivilianSuspectMoveSubmenu"),
                    new CommandStep("3", "Stop", "GameplayNoMenu")
                ],
                StateVariants:
                [
                    new CommandPlanVariant(
                        "CivilianSuspectMoveSubmenu",
                        [new CommandStep("3", "Stop", "GameplayNoMenu")])
                ]);
        }

        if (tokens.Length == 1 && HasAny(tokens, "here"))
        {
            return new CommandPlan(
                "CivilianSuspectMoveHere",
                "CivilianSuspectMoveSubmenu",
                [new CommandStep("1", "Here", "GameplayNoMenu")],
                CanOpenMenuFromClosed: false);
        }

        if (HasAny(tokens, "move", "moving") &&
            (tokens.Length == 1 || HasAny(tokens, "here")) &&
            !HasAny(tokens, "there"))
        {
            return new CommandPlan(
                "MoveContextual",
                "GroundCommandMenu",
                [new CommandStep("1", "MoveTo")],
                StateVariants:
                [
                    new CommandPlanVariant(
                        "CivilianSuspectCommandMenu",
                        [new CommandStep("2", "Move", "CivilianSuspectMoveSubmenu")])
                ]);
        }

        if (HasPhrase(normalized, "move to") || HasAny(tokens, "move", "moving", "go") && HasAny(tokens, "there"))
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
                : HasAny(tokens, "wedge") ? "4"
                : "1";

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

    private static string? ParseTeamSelection(string[] tokens)
    {
        if (HasAny(tokens, "red", "read"))
        {
            return "Red";
        }

        if (HasAny(tokens, "blue", "blew"))
        {
            return "Blue";
        }

        if (HasAny(tokens, "gold", "golden", "all", "everyone", "everybody"))
        {
            return "Gold";
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
            : HasAny(tokens, "c2", "c4", "charge", "charges", "explosive", "explosives", "boom") ? "3"
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

    private static bool HasClearMethod(string[] tokens)
    {
        return HasAny(
            tokens,
            "flash", "flashbang", "stinger", "sting", "cs", "gas", "tear",
            "9bang", "ninebang", "nine", "bang", "launcher", "launch", "grenade",
            "leader", "lead");
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
            .Replace("nine bang", "ninebang")
            .Replace("get behind me", "fall in")
            .Replace("on me", "fall in")
            .Replace("falling in", "fall in")
            .Replace("falling", "fall in");

        normalized = Regex.Replace(normalized, "\\ball in\\b", "fall in");

        normalized = NonWords.Replace(normalized, " ");
        var tokens = Whitespace.Split(normalized)
            .Where(token => token.Length > 0)
            .Where(token => !FillerWords.Contains(token, StringComparer.OrdinalIgnoreCase));

        return string.Join(' ', tokens);
    }
}
