using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VoiceReady.Core.Memory;

public sealed class ProcessMemoryReader : IDisposable
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint ProcessVmRead = 0x0010;

    private readonly Process _process;
    private readonly IntPtr _handle;
    private readonly Dictionary<string, IntPtr> _moduleBases;

    private ProcessMemoryReader(Process process, IntPtr handle)
    {
        _process = process;
        _handle = handle;
        _moduleBases = process.Modules
            .Cast<ProcessModule>()
            .GroupBy(module => module.ModuleName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().BaseAddress, StringComparer.OrdinalIgnoreCase);
    }

    public int ProcessId => _process.Id;

    public string ProcessName => _process.ProcessName;

    public static ProcessMemoryReader AttachByProcessName(params string[] processNames)
    {
        foreach (var processName in processNames)
        {
            var process = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName)).FirstOrDefault();
            if (process is null)
            {
                continue;
            }

            var handle = OpenProcess(ProcessQueryLimitedInformation | ProcessVmRead, false, process.Id);
            if (handle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not open process {process.ProcessName} ({process.Id}).");
            }

            return new ProcessMemoryReader(process, handle);
        }

        throw new InvalidOperationException($"No matching process found: {string.Join(", ", processNames)}");
    }

    public PointerReadResult ReadInt32(PointerPath pointer)
    {
        try
        {
            var address = ResolvePointer(pointer);
            var value = ReadInt32(address);

            return PointerReadResult.Succeeded(pointer, address, value);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return PointerReadResult.Failed(pointer, ex.Message);
        }
    }

    public IntPtr ResolvePointer(PointerPath pointer)
    {
        if (!_moduleBases.TryGetValue(pointer.ModuleName, out var moduleBase))
        {
            throw new InvalidOperationException($"Module not found in process: {pointer.ModuleName}");
        }

        var address = IntPtr.Add(moduleBase, checked((int)pointer.BaseOffset));
        var offsets = pointer.OffsetOrder == PointerOffsetOrder.CheatEnginePointerScanner
            ? pointer.Offsets.Reverse()
            : pointer.Offsets;

        foreach (var offset in offsets)
        {
            var next = ReadIntPtr(address);
            address = IntPtr.Add(next, checked((int)offset));
        }

        return address;
    }

    private int ReadInt32(IntPtr address)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        ReadBytes(address, buffer);

        return BitConverter.ToInt32(buffer);
    }

    private IntPtr ReadIntPtr(IntPtr address)
    {
        Span<byte> buffer = stackalloc byte[IntPtr.Size];
        ReadBytes(address, buffer);

        return IntPtr.Size == 8
            ? new IntPtr(BitConverter.ToInt64(buffer))
            : new IntPtr(BitConverter.ToInt32(buffer));
    }

    private void ReadBytes(IntPtr address, Span<byte> destination)
    {
        var buffer = new byte[destination.Length];
        if (!ReadProcessMemory(_handle, address, buffer, buffer.Length, out var bytesRead) || bytesRead != buffer.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not read {buffer.Length} bytes at 0x{address.ToInt64():X}.");
        }

        buffer.CopyTo(destination);
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            CloseHandle(_handle);
        }

        _process.Dispose();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        IntPtr processHandle,
        IntPtr baseAddress,
        [Out] byte[] buffer,
        int size,
        out int bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
