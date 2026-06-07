using System.ComponentModel;
using System.Runtime.InteropServices;
using VoiceReady.Core.Configuration;

namespace VoiceReady.Core.Input;

public sealed class KeyboardInput
{
    private const int VirtualKeyLeftShift = 0xA0;
    private const int VirtualKeyRightShift = 0xA1;
    private const ushort LeftShiftScanCode = 0x2A;
    private const ushort RightShiftScanCode = 0x36;
    private const uint InputKeyboard = 1;
    private const uint InputMouse = 0;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint KeyEventFScanCode = 0x0008;
    private const uint MouseEventFMiddleDown = 0x0020;
    private const uint MouseEventFMiddleUp = 0x0040;
    private const uint MouseEventFRightDown = 0x0008;
    private const uint MouseEventFRightUp = 0x0010;
    private const uint MouseEventFXDown = 0x0080;
    private const uint MouseEventFXUp = 0x0100;
    private const uint MouseEventFWheel = 0x0800;
    private const uint XButton1 = 0x0001;
    private const uint XButton2 = 0x0002;

    public void TapInput(InputBinding binding, TimeSpan holdDuration)
    {
        if (binding.Kind.Equals("Keyboard", StringComparison.OrdinalIgnoreCase))
        {
            TapScanCode(binding.ScanCodeValue, holdDuration);
            return;
        }

        if (binding.Kind.Equals("MouseMiddle", StringComparison.OrdinalIgnoreCase))
        {
            TapMiddleMouse(holdDuration);
            return;
        }

        if (binding.Kind.Equals("MouseRight", StringComparison.OrdinalIgnoreCase))
        {
            SendMouse(MouseEventFRightDown);
            Thread.Sleep(holdDuration);
            SendMouse(MouseEventFRightUp);
            return;
        }

        if (binding.Kind.Equals("MouseX1", StringComparison.OrdinalIgnoreCase))
        {
            SendMouse(MouseEventFXDown, XButton1);
            Thread.Sleep(holdDuration);
            SendMouse(MouseEventFXUp, XButton1);
            return;
        }

        if (binding.Kind.Equals("MouseX2", StringComparison.OrdinalIgnoreCase))
        {
            SendMouse(MouseEventFXDown, XButton2);
            Thread.Sleep(holdDuration);
            SendMouse(MouseEventFXUp, XButton2);
            return;
        }

        throw new NotSupportedException($"Unsupported input binding: {binding.Kind}");
    }

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

    public void ScrollWheel(int delta)
    {
        SendMouse(MouseEventFWheel, unchecked((uint)delta));
    }

    public IDisposable ReleaseShiftIfPressed()
    {
        var leftShiftDown = IsKeyDown(VirtualKeyLeftShift);
        var rightShiftDown = IsKeyDown(VirtualKeyRightShift);

        if (leftShiftDown)
        {
            Send(LeftShiftScanCode, keyUp: true);
        }

        if (rightShiftDown)
        {
            Send(RightShiftScanCode, keyUp: true);
        }

        return new ShiftReleaseScope(leftShiftDown, rightShiftDown);
    }

    private static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

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

    private static void SendMouse(uint flags, uint mouseData = 0)
    {
        var input = new INPUT
        {
            type = InputMouse,
            union = new InputUnion
            {
                mouse = new MOUSEINPUT
                {
                    dwFlags = flags,
                    mouseData = mouseData
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

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    private sealed class ShiftReleaseScope(bool restoreLeftShift, bool restoreRightShift) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (restoreLeftShift)
            {
                Send(LeftShiftScanCode, keyUp: false);
            }

            if (restoreRightShift)
            {
                Send(RightShiftScanCode, keyUp: false);
            }
        }
    }

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
