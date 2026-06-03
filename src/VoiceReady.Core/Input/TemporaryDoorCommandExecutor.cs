using VoiceReady.Core.Detection;

namespace VoiceReady.Core.Input;

public sealed class TemporaryDoorCommandExecutor
{
    public const int DoorCommandMenuValue = 26673452;
    public const int DoorBreachSubmenuValue = 21627180;
    public const int DoorOpenSubmenuValue = 26476844;

    private static readonly TimeSpan KeyHoldDuration = TimeSpan.FromMilliseconds(35);
    private static readonly TimeSpan StepTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(25);

    private readonly MenuStateReader _menuStateReader;
    private readonly KeyboardInput _keyboardInput;

    public TemporaryDoorCommandExecutor(MenuStateReader menuStateReader, KeyboardInput keyboardInput)
    {
        _menuStateReader = menuStateReader;
        _keyboardInput = keyboardInput;
    }

    public bool TryExecuteBreachC2Clear(out string message)
    {
        var initialState = _menuStateReader.Read();
        if (!IsState(initialState, DoorCommandMenuValue))
        {
            message = $"Door menu is not active. Current value: {initialState.VotedValue?.ToString() ?? "no consensus"}.";
            return false;
        }

        _keyboardInput.TapScanCode(NumberRowScanCodes.Three, KeyHoldDuration);
        if (!WaitForState(DoorBreachSubmenuValue))
        {
            message = "Pressed Breach, but DoorBreachSubmenu was not detected.";
            return false;
        }

        _keyboardInput.TapScanCode(NumberRowScanCodes.Three, KeyHoldDuration);
        if (!WaitForState(DoorOpenSubmenuValue))
        {
            message = "Pressed C2, but DoorOpenSubmenu was not detected.";
            return false;
        }

        _keyboardInput.TapScanCode(NumberRowScanCodes.One, KeyHoldDuration);
        message = "Executed test command: Breach -> C2 -> Clear.";
        return true;
    }

    private bool WaitForState(int expectedValue)
    {
        var deadline = DateTimeOffset.UtcNow + StepTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (IsState(_menuStateReader.Read(), expectedValue))
            {
                return true;
            }

            Thread.Sleep(PollInterval);
        }

        return false;
    }

    private static bool IsState(MenuStateSnapshot snapshot, int expectedValue)
    {
        return snapshot.IsReliable && snapshot.VotedValue == expectedValue;
    }
}
