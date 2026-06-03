using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VoiceReady.Core.Input;

public sealed class KeyboardInput
{
    private const uint InputKeyboard = 1;
    private const uint InputMouse = 0;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint KeyEventFScanCode = 0x0008;
    private const uint MouseEventFMiddleDown = 0x0020;
    private const uint MouseEventFMiddleUp = 0x0040;

    public void TapScanCode(ushort scanCode, TimeSpan holdDuration)
    {
        Send(scanCode, keyUp: false);
        Thread.Sleep(holdDuration);
        Send(scanCode, keyUp: true);
    }

    public void TapMiddleMouse(TimeSpan holdDuration)
    {
        SendMouse(MouseEventFMiddleDown);
        Thread.Sleep(holdDuration);
        SendMouse(MouseEventFMiddleUp);
    }

    private static void Send(ushort scanCode, bool keyUp)
    {
        var input = new INPUT
        {
            type = InputKeyboard,
            union = new InputUnion
            {
                keyboard = new KEYBDINPUT
                {
                    wScan = scanCode,
                    dwFlags = KeyEventFScanCode | (keyUp ? KeyEventFKeyUp : 0)
                }
            }
        };

        var sent = SendInput(1, [input], Marshal.SizeOf<INPUT>());
        if (sent != 1)
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, $"SendInput failed with Win32 error {error}.");
        }
    }

    private static void SendMouse(uint flags)
    {
        var input = new INPUT
        {
            type = InputMouse,
            union = new InputUnion
            {
                mouse = new MOUSEINPUT
                {
                    dwFlags = flags
                }
            }
        };

        var sent = SendInput(1, [input], Marshal.SizeOf<INPUT>());
        if (sent != 1)
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, $"SendInput mouse failed with Win32 error {error}.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, INPUT[] inputs, int inputSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT keyboard;

        [FieldOffset(0)]
        public MOUSEINPUT mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
