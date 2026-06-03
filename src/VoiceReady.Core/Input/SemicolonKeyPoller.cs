using System.Runtime.InteropServices;

namespace VoiceReady.Core.Input;

public sealed class SemicolonKeyPoller
{
    private const int VkOem1 = 0xBA;

    private bool _wasDown;

    public bool WasPressed()
    {
        var isDown = IsKeyDown(VkOem1);
        var wasPressed = isDown && !_wasDown;
        _wasDown = isDown;

        return wasPressed;
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
