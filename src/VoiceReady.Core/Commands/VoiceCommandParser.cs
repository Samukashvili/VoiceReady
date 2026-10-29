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
        var isFormation = HasAny(tokens, "formation", "fall", "file", "single", "double", "diamond") || HasPhrase(normalized, "form up");

        if (HasAny(tokens, "breach", "breaching", "reach", "bridge", "breech", "break", "broke", "blow") ||
            HasAny(tokens, "kick", "kicking", "shotgun", "c2", "c4", "charge", "explosive", "explosives", "ram") &&
            (HasAny(tokens, "door", "clear", "in", "onmygo") || tokens.Length == 1))
        {
            return ParseDoorBreach(tokens);
        }

        if (HasAny(tokens, "disarm", "disarming", "defuse", "diffuse", "disable") ||
            tokens.Length == 1 && HasAny(tokens, "trap") ||
            HasAny(tokens, "remove", "cut") && HasAny(tokens, "trap", "wire", "tripwire", "bomb"))
        {
            return new CommandPlan(
                "DoorDisarmTrap",
                "DoorCommandMenu",
                [new CommandStep("6", "DisarmTrap")]);
        }

        if (HasAny(tokens, "remove", "unblock", "unjam", "unwedge") && HasAny(tokens, "wedge", "jam", "jammed", "block", "blocked", "door", "down", "time") ||
            HasAny(tokens, "pull", "take") && HasAny(tokens, "wedge", "jam"))
        {
            return new CommandPlan(
                "DoorRemoveWedge",
                "DoorCommandMenu",
                [new CommandStep("6", "RemoveWedge")]);
        }

        if (HasAny(tokens, "wedge", "jam", "jammed", "block", "blocked") && !isFormation ||
            HasAny(tokens, "where") && HasAny(tokens, "door"))
        {
            return new CommandPlan(
                "DoorWedge",
                "DoorCommandMenu",
                [new CommandStep("6", "WedgeDoor")]);
        }

        if (HasPhrase(normalized, "stop focus") || HasAny(tokens, "unfocus", "defocus"))
        {
            return IndividualSwatFocus("IndividualSwatUnfocus", "5", "Unfocus");
        }

        if (HasAny(tokens, "focus", "focusing"))
        {
            return HasPhrase(normalized, "my position") || HasAny(tokens, "me") ? IndividualSwatFocus("IndividualSwatFocusMyPosition", "2", "MyPosition")
                : HasAny(tokens, "door") ? IndividualSwatFocus("IndividualSwatFocusDoor", "3", "Door")
                : HasAny(tokens, "target", "suspect", "guy", "person") ? IndividualSwatFocus("IndividualSwatFocusTarget", "4", "Target")
                : IndividualSwatFocus("IndividualSwatFocusHere", "1", "Here");
        }

        if (HasAny(tokens, "swap", "switch", "trade") && HasAny(tokens, "alpha", "alfa", "charlie", "bravo", "delta"))
        {
            var swapKey = HasAny(tokens, "alpha", "alfa") ? "1"
                : HasAny(tokens, "charlie") ? "2"
                : HasAny(tokens, "bravo") ? "3"
                : "4";

            return new CommandPlan(
                "IndividualSwatSwapWith",
                "IndividualSwatCommandMenu",
                [
                    new CommandStep("3", "SwapWith", "IndividualSwatSwapWithSubmenu"),
                    new CommandStep(swapKey, "SwapTarget")
                ],
                StateVariants:
                [
                    new CommandPlanVariant("IndividualSwatSwapWithSubmenu", [new CommandStep(swapKey, "SwapTarget")])
                ]);
        }

        if (HasAny(tokens, "search", "sweep", "secure"))
        {
            return new CommandPlan(
                "GroundSearchArea",
                "GroundCommandMenu",
                [new CommandStep("6", "SearchArea")],
                StateVariants:
                [
                    new CommandPlanVariant("IndividualSwatCommandMenu", [new CommandStep("4", "SearchRoom")])
                ]);
        }

        if (HasAny(tokens, "restrain", "restraining", "restraying", "arrest", "arresting", "aristing", "cuff", "cuffs", "handcuff", "restoring", "detain", "zip", "tie"))
        {
            return new CommandPlan(
                "CivilianSuspectRestrain",
                "CivilianSuspectCommandMenu",
                [new CommandStep("1", "Restrain")]);
        }

        if ((HasAny(tokens, "turn") && HasAny(tokens, "around", "road", "round")) || HasPhrase(normalized, "face away") || HasPhrase(normalized, "face wall"))
        {
            return new CommandPlan(
                "CivilianSuspectTurnAround",
                "CivilianSuspectCommandMenu",
                [new CommandStep("4", "TurnAround")]);
        }

        if (HasAny(tokens, "exit", "escort"))
        {
            return new CommandPlan(
                "CivilianSuspectMoveToExit",
                "CivilianSuspectCommandMenu",
                [new CommandStep("5", "MoveToExit")]);
        }

        if (HasAny(tokens, "mirror") || HasPhrase(normalized, "under door") || HasPhrase(normalized, "look under") || HasPhrase(normalized, "check under"))
        {
            return new CommandPlan(
                "DoorMirrorUnderDoor",
                "DoorCommandMenu",
                [new CommandStep("5", "MirrorUnderDoor")]);
        }

        if (HasAny(tokens, "pick") && (HasAny(tokens, "lock", "door") || tokens.Length == 1))
        {
            return new CommandPlan(
                "DoorPickLock",
                "DoorCommandMenu",
                [new CommandStep("2", "PickLock")]);
        }

        if (HasAny(tokens, "open", "close", "shut") && (HasAny(tokens, "door") || tokens.Length == 1))
        {
            return new CommandPlan(
                HasAny(tokens, "close", "shut") ? "DoorClose" : "DoorOpen",
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

        if (HasAny(tokens, "clear", "onmygo") ||
            HasPhrase(normalized, "go in") ||
            HasPhrase(normalized, "going in") ||
            HasPhrase(normalized, "move in") ||
            HasAny(tokens, "move", "moving", "enter") && HasClearMethod(tokens))
        {
            return ParseClear(tokens);
        }

        if (HasPhrase(normalized, "stack up") || HasAny(tokens, "stack") || HasPhrase(normalized, "line up"))
        {
            var stackKey = HasAny(tokens, "left") ? "2"
                : HasAny(tokens, "right") ? "3"
                : HasAny(tokens, "split") ? "1"
                : "4";

            return new CommandPlan(
                "DoorStackUp",
                "DoorCommandMenu",
                [
                    new CommandStep("1", "StackUp", "DoorStackUpSubmenu", CompletesIfMenuCloses: true),
                    new CommandStep(stackKey, "StackMode")
                ],
                AlternativeInitialStates: ["DoorwayCommandMenu"]);
        }

        // "Use the stinger": clear with it at a door or doorway, deploy it when aiming at the ground.
        if (HasClearItem(tokens) && !HasAny(tokens, "deploy", "throw", "toss", "drop", "place", "chem", "chemlight", "light", "shield"))
        {
            return new CommandPlan(
                "UseItemContextual",
                "DoorCommandMenu",
                [
                    new CommandStep("2", "OpenOrMove", "DoorOpenSubmenu"),
                    new CommandStep(GetClearKey(tokens), "ClearMethod")
                ],
                AlternativeInitialStates: ["DoorwayCommandMenu"],
                StateVariants:
                [
                    new CommandPlanVariant(
                        "GroundCommandMenu",
                        [
                            new CommandStep("5", "Deploy", "GroundDeploySubmenu"),
                            new CommandStep(GetDeployKey(tokens), "Equipment")
                        ])
                ]);
        }

        if (HasAny(tokens, "deploy", "chem", "chemlight", "shield") || HasAny(tokens, "throw", "toss", "drop", "place") && HasDeployItem(tokens))
        {
            var deployKey = GetDeployKey(tokens);

            return new CommandPlan(
                "GroundDeploy",
                "GroundCommandMenu",
                [
                    new CommandStep("5", "Deploy", "GroundDeploySubmenu"),
                    new CommandStep(deployKey, "Equipment")
                ]);
        }

        if (HasAny(tokens, "cover", "watch"))
        {
            return new CommandPlan(
                "GroundCover",
                "GroundCommandMenu",
                [new CommandStep("3", "Cover")]);
        }

        if (HasAny(tokens, "hold", "stay", "wait"))
        {
            return new CommandPlan(
                "GroundHold",
                "GroundCommandMenu",
                [new CommandStep("4", "Hold")]);
        }

        if (HasPhrase(normalized, "my position") || HasAny(tokens, "come") && HasAny(tokens, "me", "here"))
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

        if (HasAny(tokens, "move", "moving", "go") && HasAny(tokens, "back"))
        {
            return new CommandPlan(
                "IndividualSwatMoveHereThenBack",
                "IndividualSwatCommandMenu",
                [
                    new CommandStep("1", "Move", "IndividualSwatMoveSubmenu"),
                    new CommandStep("2", "HereThenBack")
                ],
                StateVariants:
                [
                    new CommandPlanVariant("IndividualSwatMoveSubmenu", [new CommandStep("2", "HereThenBack")])
                ]);
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
                        [new CommandStep("2", "Move", "CivilianSuspectMoveSubmenu")]),
                    new CommandPlanVariant(
                        "IndividualSwatCommandMenu",
                        [
                            new CommandStep("1", "Move", "IndividualSwatMoveSubmenu"),
                            new CommandStep("1", "Here")
                        ]),
                    new CommandPlanVariant("IndividualSwatMoveSubmenu", [new CommandStep("1", "Here")])
                ]);
        }

        if (HasPhrase(normalized, "move to") || HasPhrase(normalized, "move up") || HasAny(tokens, "move", "moving", "go") && HasAny(tokens, "there", "here"))
        {
            return new CommandPlan(
                "GroundMoveTo",
                "GroundCommandMenu",
                [new CommandStep("1", "MoveTo")],
                StateVariants:
                [
                    new CommandPlanVariant(
                        "IndividualSwatCommandMenu",
                        [
                            new CommandStep("1", "Move", "IndividualSwatMoveSubmenu"),
                            new CommandStep("1", "Here")
                        ])
                ]);
        }

        if (isFormation || HasAny(tokens, "folding", "regroup", "follow") || HasPhrase(normalized, "wedge formation"))
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

    private static CommandPlan IndividualSwatFocus(string name, string key, string stepName)
    {
        return new CommandPlan(
            name,
            "IndividualSwatCommandMenu",
            [
                new CommandStep("2", "Focus", "IndividualSwatFocusSubmenu"),
                new CommandStep(key, stepName)
            ],
            StateVariants:
            [
                new CommandPlanVariant("IndividualSwatFocusSubmenu", [new CommandStep(key, stepName)])
            ]);
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
            : HasAny(tokens, "c2", "c4", "charge", "charges", "explosive", "explosives", "boom", "blow") ? "3"
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
            : HasAny(tokens, "leader", "lead", "onmygo") ? "7"
            : "1";
    }

    private static bool HasClearMethod(string[] tokens)
    {
        return HasAny(
            tokens,
            "flash", "flashbang", "stinger", "sting", "cs", "gas", "tear",
            "9bang", "ninebang", "nine", "bang", "launcher", "launch", "grenade",
            "leader", "lead", "onmygo");
    }

    private static string GetDeployKey(string[] tokens)
    {
        return HasAny(tokens, "flash", "flashbang") ? "1"
            : HasAny(tokens, "stinger", "sting") ? "2"
            : HasAny(tokens, "cs", "gas", "tear") ? "3"
            : HasAny(tokens, "9bang", "ninebang", "nine", "bang") ? "4"
            : HasAny(tokens, "chem", "chemlight", "light") ? "5"
            : HasAny(tokens, "shield") ? "6"
            : "5";
    }

    private static bool HasClearItem(string[] tokens)
    {
        return HasAny(tokens, "flash", "flashbang", "stinger", "sting", "cs", "gas", "tear", "9bang", "ninebang", "nine", "bang");
    }

    private static bool HasDeployItem(string[] tokens)
    {
        return HasAny(
            tokens,
            "flash", "flashbang", "stinger", "sting", "cs", "gas", "tear",
            "9bang", "ninebang", "nine", "bang", "chem", "chemlight", "light", "shield");
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
            .Replace("flash bang", "flashbang")
            .Replace("lock pick", "pick lock")
            .Replace("lockpick", "pick lock")
            // "On my go" means the Leader clear: the team waits for the player to enter first.
            // It gets its own token so it only picks the clear method, never the "leader" breach method.
            .Replace("on my go", " onmygo ")
            .Replace("on my lead", " onmygo ")
            .Replace("on my command", " onmygo ")
            .Replace("follow my lead", " onmygo ")
            .Replace("follow me in", " onmygo ")
            .Replace("wait for me", " onmygo ")
            .Replace("focus on me", "focus my position")
            .Replace("get behind me", "fall in")
            .Replace("on me", "fall in")
            .Replace("falling in", "fall in")
            .Replace("falling", "fall in")
            .Replace("fallen", "fall in");

        normalized = Regex.Replace(normalized, "\\ball in\\b", "fall in");

        normalized = NonWords.Replace(normalized, " ");
        var tokens = Whitespace.Split(normalized)
            .Where(token => token.Length > 0)
            .Where(token => !FillerWords.Contains(token, StringComparer.OrdinalIgnoreCase));

        return string.Join(' ', tokens);
    }
}
