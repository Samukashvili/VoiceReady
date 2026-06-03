using VoiceReady.Core.Configuration;
using VoiceReady.Core.Detection;
using VoiceReady.Core.Input;

namespace VoiceReady.Core.Commands;

public sealed class CommandPlanExecutor
{
    private const string ClosedStateName = "GameplayNoMenu";

    private readonly MenuStateReader _menuStateReader;
    private readonly IReadOnlyDictionary<string, int> _stateValuesByName;
    private readonly KeyboardInput _keyboardInput;
    private readonly InputSettings _settings;

    public CommandPlanExecutor(
        MenuStateReader menuStateReader,
        IEnumerable<KnownMenuState> knownStates,
        KeyboardInput keyboardInput,
        InputSettings settings)
    {
        _menuStateReader = menuStateReader;
        _keyboardInput = keyboardInput;
        _settings = settings;
        _stateValuesByName = knownStates
            .SelectMany(state => new[] { state.Name }.Concat(state.Aliases).Select(name => new KeyValuePair<string, int>(name, state.Value)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryExecute(CommandPlan plan, out string message)
    {
        if (!_stateValuesByName.TryGetValue(plan.RequiredInitialState, out var requiredStateValue))
        {
            message = $"Unknown required state: {plan.RequiredInitialState}.";
            return false;
        }

        var allowedInitialStates = new List<(string Name, int Value)> { (plan.RequiredInitialState, requiredStateValue) };
        foreach (var stateName in plan.AlternativeInitialStates ?? [])
        {
            if (_stateValuesByName.TryGetValue(stateName, out var value))
            {
                allowedInitialStates.Add((stateName, value));
            }
        }

        var snapshot = _menuStateReader.Read();
        if (!snapshot.IsReliable)
        {
            message = "Menu state is not reliable enough to execute.";
            return false;
        }

        if (IsClosed(snapshot) && plan.CanOpenMenuFromClosed)
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
        else if (!allowedInitialStates.Any(state => snapshot.VotedValue == state.Value))
        {
            message = $"Wrong menu state. Expected {string.Join(" or ", allowedInitialStates.Select(state => state.Name))}, current value {snapshot.VotedValue}.";
            return false;
        }

        foreach (var step in plan.Steps)
        {
            TapNumberKey(step.Key);
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
                CloseIfOpen();
                message = $"Step {step.Name} did not transition to {step.ExpectedStateAfter}.";
                return false;
            }
        }

        message = $"Executed {plan.Name}.";
        return true;
    }

    private bool IsClosed(MenuStateSnapshot snapshot)
    {
        return _stateValuesByName.TryGetValue(ClosedStateName, out var closedValue) && snapshot.VotedValue == closedValue;
    }

    private void TapCommandMenuOpen()
    {
        if (_settings.CommandMenuOpen.Kind.Equals("MouseMiddle", StringComparison.OrdinalIgnoreCase))
        {
            _keyboardInput.TapMiddleMouse(TimeSpan.FromMilliseconds(_settings.KeyHoldMilliseconds));
            return;
        }

        throw new NotSupportedException($"Unsupported command menu open input: {_settings.CommandMenuOpen.Kind}");
    }

    private void CloseIfOpen()
    {
        var snapshot = _menuStateReader.Read();
        if (snapshot.IsReliable && !IsClosed(snapshot))
        {
            _keyboardInput.TapScanCode(_settings.CloseMenuScanCodeValue, TimeSpan.FromMilliseconds(_settings.KeyHoldMilliseconds));
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

    private void TapNumberKey(string key)
    {
        var scanCode = key switch
        {
            "1" => NumberRowScanCodes.One,
            "2" => NumberRowScanCodes.Two,
            "3" => NumberRowScanCodes.Three,
            "4" => NumberRowScanCodes.Four,
            "5" => NumberRowScanCodes.Five,
            "6" => NumberRowScanCodes.Six,
            "7" => NumberRowScanCodes.Seven,
            "8" => NumberRowScanCodes.Eight,
            "9" => NumberRowScanCodes.Nine,
            "0" => NumberRowScanCodes.Zero,
            _ => throw new NotSupportedException($"Unsupported command key: {key}")
        };

        _keyboardInput.TapScanCode(scanCode, TimeSpan.FromMilliseconds(_settings.KeyHoldMilliseconds));
    }
}
