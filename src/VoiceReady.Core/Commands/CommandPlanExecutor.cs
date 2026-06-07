using VoiceReady.Core.Configuration;
using VoiceReady.Core.Detection;
using VoiceReady.Core.Input;

namespace VoiceReady.Core.Commands;

public sealed class CommandPlanExecutor
{
    private const string ClosedStateName = "GameplayNoMenu";
    private const string InteractionPromptStateName = "InteractionPrompt";
    private const string DoorCommandMenuStateName = "DoorCommandMenu";
    private const string TrappedDoorCommandMenuStateName = "TrappedDoorCommandMenu";

    private readonly MenuStateReader _menuStateReader;
    private readonly MenuStateReader _teamSelectionReader;
    private readonly IReadOnlyDictionary<string, int> _stateValuesByName;
    private readonly IReadOnlyDictionary<string, int> _teamValuesByName;
    private readonly KeyboardInput _keyboardInput;
    private readonly InputSettings _settings;

    public CommandPlanExecutor(
        MenuStateReader menuStateReader,
        IEnumerable<KnownMenuState> knownStates,
        MenuStateReader teamSelectionReader,
        IEnumerable<KnownTeamSelection> knownTeamSelections,
        KeyboardInput keyboardInput,
        InputSettings settings)
    {
        _menuStateReader = menuStateReader;
        _teamSelectionReader = teamSelectionReader;
        _keyboardInput = keyboardInput;
        _settings = settings;
        _stateValuesByName = knownStates
            .SelectMany(state => new[] { state.Name }.Concat(state.Aliases).Select(name => new KeyValuePair<string, int>(name, state.Value)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        _teamValuesByName = knownTeamSelections
            .SelectMany(selection => new[] { selection.Name }.Concat(selection.Aliases)
                .Select(name => new KeyValuePair<string, int>(name, selection.Value)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryExecute(CommandPlan plan, out string message)
    {
        using var shiftRelease = _keyboardInput.ReleaseShiftIfPressed();
        return TryExecuteCore(plan, out message);
    }

    private bool TryExecuteCore(CommandPlan plan, out string message)
    {
        if (plan.Steps.Count == 0 && !string.IsNullOrWhiteSpace(plan.TeamSelection))
        {
            return TrySelectTeamOnly(plan.TeamSelection, out message);
        }

        if (!_stateValuesByName.TryGetValue(plan.RequiredInitialState, out var requiredStateValue))
        {
            message = $"Unknown required state: {plan.RequiredInitialState}.";
            return false;
        }

        var allowedInitialStates = new List<(string Name, int Value)> { (plan.RequiredInitialState, requiredStateValue) };
        if (plan.RequiredInitialState.Equals(DoorCommandMenuStateName, StringComparison.OrdinalIgnoreCase) &&
            _stateValuesByName.TryGetValue(TrappedDoorCommandMenuStateName, out var trappedDoorValue))
        {
            allowedInitialStates.Add((TrappedDoorCommandMenuStateName, trappedDoorValue));
        }

        foreach (var stateName in plan.AlternativeInitialStates ?? [])
        {
            if (_stateValuesByName.TryGetValue(stateName, out var value))
            {
                allowedInitialStates.Add((stateName, value));
            }
        }
        foreach (var variant in plan.StateVariants ?? [])
        {
            if (_stateValuesByName.TryGetValue(variant.InitialState, out var value) &&
                allowedInitialStates.All(state => state.Value != value))
            {
                allowedInitialStates.Add((variant.InitialState, value));
            }
        }

        var snapshot = _menuStateReader.Read();
        if (!snapshot.IsReliable)
        {
            message = "Menu state is not reliable enough to execute.";
            return false;
        }

        var isDoorCommandPlan = plan.RequiredInitialState.Equals(DoorCommandMenuStateName, StringComparison.OrdinalIgnoreCase);
        var isInteractionOverlayFallback = false;
        if (snapshot.VotedValue.HasValue && allowedInitialStates.Any(state => state.Value == snapshot.VotedValue.Value))
        {
            requiredStateValue = snapshot.VotedValue.Value;
        }
        else if (IsClosed(snapshot) && plan.CanOpenMenuFromClosed)
        {
            TapCommandMenuOpen();
            if (!WaitForAnyState(allowedInitialStates.Select(state => state.Value), out var matchedState))
            {
                CloseIfOpen();
                message = $"Opened command menu, but expected {string.Join(" or ", allowedInitialStates.Select(state => state.Name))} did not appear.";
                return false;
            }

            requiredStateValue = matchedState;
        }
        else if (IsInteractionPrompt(snapshot) && isDoorCommandPlan && plan.CanOpenMenuFromClosed)
        {
            TapCommandMenuOpen();
            if (WaitForAnyState(allowedInitialStates.Select(state => state.Value), out var matchedState))
            {
                requiredStateValue = matchedState;
            }
            else
            {
                requiredStateValue = ResolveInteractionOverlayFallbackState(plan, requiredStateValue);
                isInteractionOverlayFallback = true;
            }
        }
        else if (!allowedInitialStates.Any(state => snapshot.VotedValue == state.Value))
        {
            message = $"Wrong menu state. Expected {string.Join(" or ", allowedInitialStates.Select(state => state.Name))}, current value {snapshot.VotedValue}.";
            return false;
        }
        else
        {
            requiredStateValue = snapshot.VotedValue!.Value;
        }

        if (!string.IsNullOrWhiteSpace(plan.TeamSelection) && !TrySelectTeam(plan.TeamSelection, out var teamMessage))
        {
            CloseIfOpen();
            message = teamMessage;
            return false;
        }

        var steps = ResolveSteps(plan, requiredStateValue);
        var isTrappedDoorCommandMenu = IsState(requiredStateValue, TrappedDoorCommandMenuStateName);

        foreach (var step in steps)
        {
            if (step.Name.Equals("DisarmTrap", StringComparison.OrdinalIgnoreCase) && !isTrappedDoorCommandMenu)
            {
                CloseIfOpen();
                message = "Current door menu does not indicate an identified trap.";
                return false;
            }

            TapNumberKey(ResolveTrapAwareKey(step, isTrappedDoorCommandMenu));
            Thread.Sleep(_settings.BetweenKeysMilliseconds);

            if (string.IsNullOrWhiteSpace(step.ExpectedStateAfter))
            {
                continue;
            }

            if (!_stateValuesByName.TryGetValue(step.ExpectedStateAfter, out var expectedStateValue))
            {
                CloseIfOpen();
                message = $"Unknown expected state after {step.Name}: {step.ExpectedStateAfter}.";
                return false;
            }

            if (!WaitForState(expectedStateValue))
            {
                if (!isInteractionOverlayFallback)
                {
                    CloseIfOpen();
                    message = $"Step {step.Name} did not transition to {step.ExpectedStateAfter}.";
                    return false;
                }
            }
        }

        message = $"Executed {plan.Name}.";
        return true;
    }

    private IReadOnlyList<CommandStep> ResolveSteps(CommandPlan plan, int initialStateValue)
    {
        foreach (var variant in plan.StateVariants ?? [])
        {
            if (_stateValuesByName.TryGetValue(variant.InitialState, out var variantValue) &&
                variantValue == initialStateValue)
            {
                return variant.Steps;
            }
        }

        return plan.Steps;
    }

    private string ResolveTrapAwareKey(CommandStep step, bool isTrappedDoorCommandMenu)
    {
        if (!isTrappedDoorCommandMenu)
        {
            return step.Key;
        }

        return step.Name switch
        {
            "WedgeDoor" => "7",
            "RemoveWedge" => "7",
            "OpenOrCloseDoor" => "9",
            _ => step.Key
        };
    }

    private bool IsState(int stateValue, string stateName)
    {
        return _stateValuesByName.TryGetValue(stateName, out var expectedValue) && stateValue == expectedValue;
    }

    private bool TrySelectTeamOnly(string teamSelection, out string message)
    {
        var snapshot = _menuStateReader.Read();
        if (!snapshot.IsReliable || !IsClosed(snapshot))
        {
            message = "Team-only selection requires gameplay with no menu open.";
            return false;
        }

        TapCommandMenuOpen();
        if (!WaitForOpenMenu())
        {
            CloseIfOpen();
            message = "Could not open a command menu for team selection.";
            return false;
        }

        var selected = TrySelectTeam(teamSelection, out message);
        CloseIfOpen();
        return selected;
    }

    private bool TrySelectTeam(string teamSelection, out string message)
    {
        if (!_teamValuesByName.TryGetValue(teamSelection, out var expectedValue))
        {
            message = $"Unknown team selection: {teamSelection}.";
            return false;
        }

        var snapshot = _teamSelectionReader.Read();
        if (!snapshot.IsReliable)
        {
            message = "Team selection state is not reliable enough to execute.";
            return false;
        }

        if (snapshot.VotedValue == expectedValue)
        {
            message = $"{teamSelection} team is already selected.";
            return true;
        }

        for (var attempt = 0; attempt < _settings.TeamSelectionMaximumScrolls; attempt++)
        {
            _keyboardInput.ScrollWheel(_settings.TeamSelectionWheelDelta);

            if (WaitForTeamSelection(expectedValue))
            {
                message = $"Selected {teamSelection} team.";
                return true;
            }
        }

        message = $"Could not select {teamSelection} team.";
        return false;
    }

    private bool IsClosed(MenuStateSnapshot snapshot)
    {
        return _stateValuesByName.TryGetValue(ClosedStateName, out var closedValue) && snapshot.VotedValue == closedValue;
    }

    private bool IsInteractionPrompt(MenuStateSnapshot snapshot)
    {
        return _stateValuesByName.TryGetValue(InteractionPromptStateName, out var interactionPromptValue) &&
            snapshot.VotedValue == interactionPromptValue;
    }

    private int ResolveInteractionOverlayFallbackState(CommandPlan plan, int defaultStateValue)
    {
        if (plan.Steps.Any(step => step.Name.Equals("DisarmTrap", StringComparison.OrdinalIgnoreCase)) &&
            _stateValuesByName.TryGetValue(TrappedDoorCommandMenuStateName, out var trappedDoorValue))
        {
            return trappedDoorValue;
        }

        return defaultStateValue;
    }

    private void TapCommandMenuOpen()
    {
        _keyboardInput.TapInput(_settings.CommandMenuOpen, TimeSpan.FromMilliseconds(_settings.KeyHoldMilliseconds));
    }

    private void CloseIfOpen()
    {
        var snapshot = _menuStateReader.Read();
        if (snapshot.IsReliable && !IsClosed(snapshot))
        {
            TapCommandMenuOpen();
        }
    }

    private bool WaitForState(int expectedValue)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(_settings.StateTransitionTimeoutMilliseconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var snapshot = _menuStateReader.Read();
            if (snapshot.IsReliable && snapshot.VotedValue == expectedValue)
            {
                return true;
            }

            Thread.Sleep(25);
        }

        return false;
    }

    private bool WaitForAnyState(IEnumerable<int> expectedValues, out int matchedState)
    {
        var expected = expectedValues.ToHashSet();
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(_settings.StateTransitionTimeoutMilliseconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var snapshot = _menuStateReader.Read();
            if (snapshot.IsReliable && snapshot.VotedValue.HasValue && expected.Contains(snapshot.VotedValue.Value))
            {
                matchedState = snapshot.VotedValue.Value;
                return true;
            }

            Thread.Sleep(25);
        }

        matchedState = 0;
        return false;
    }

    private bool WaitForOpenMenu()
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(_settings.StateTransitionTimeoutMilliseconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var snapshot = _menuStateReader.Read();
            if (snapshot.IsReliable && !IsClosed(snapshot))
            {
                return true;
            }

            Thread.Sleep(25);
        }

        return false;
    }

    private bool WaitForTeamSelection(int expectedValue)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(
            Math.Min(_settings.StateTransitionTimeoutMilliseconds, 250));
        while (DateTimeOffset.UtcNow < deadline)
        {
            var snapshot = _teamSelectionReader.Read();
            if (snapshot.IsReliable && snapshot.VotedValue == expectedValue)
            {
                return true;
            }

            Thread.Sleep(25);
        }

        return false;
    }

    private void TapNumberKey(string key)
    {
        _keyboardInput.TapInput(_settings.GetCommandKey(key), TimeSpan.FromMilliseconds(_settings.KeyHoldMilliseconds));
    }
}
