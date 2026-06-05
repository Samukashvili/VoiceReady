using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VoiceReady.Core.Memory;

public sealed class ProcessMemoryReader : IDisposable
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint ProcessVmRead = 0x0010;
    private const uint MemCommit = 0x1000;
    private const uint PageGuard = 0x100;
    private const uint PageNoAccess = 0x01;

    private readonly Process _process;
    private readonly IntPtr _handle;
    private readonly Dictionary<string, ProcessModule> _modules;
    private readonly Dictionary<string, byte[]> _moduleSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string ModuleName, long BaseOffset), IntPtr> _resolvedRoots = new();
    private readonly Dictionary<(string ModuleName, long BaseOffset), PointerRootResolution> _rootResolutions = new();

    private ProcessMemoryReader(Process process, IntPtr handle)
    {
        _process = process;
        _handle = handle;
        _modules = process.Modules
            .Cast<ProcessModule>()
            .GroupBy(module => module.ModuleName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public int ProcessId => _process.Id;

    public string ProcessName => _process.ProcessName;

    public IReadOnlyCollection<PointerRootResolution> RootResolutions => _rootResolutions.Values;

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
        if (!_modules.TryGetValue(pointer.ModuleName, out var module))
        {
            throw new InvalidOperationException($"Module not found in process: {pointer.ModuleName}");
        }

        var address = ResolveRoot(pointer, module);
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

    private IntPtr ResolveRoot(PointerPath pointer, ProcessModule module)
    {
        var key = (pointer.ModuleName.ToUpperInvariant(), pointer.BaseOffset);
        if (_resolvedRoots.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var fallback = IntPtr.Add(module.BaseAddress, checked((int)pointer.BaseOffset));
        if (pointer.RootSignature is null)
        {
            RecordRootResolution(key, module, pointer.BaseOffset, fallback, false, "No signature configured.");
            return fallback;
        }

        try
        {
            var relocated = ResolveSignatureRoot(module, pointer.RootSignature);
            RecordRootResolution(key, module, pointer.BaseOffset, relocated, true, null);
            return relocated;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or FormatException)
        {
            RecordRootResolution(key, module, pointer.BaseOffset, fallback, false, ex.Message);
            return fallback;
        }
    }

    private void RecordRootResolution(
        (string ModuleName, long BaseOffset) key,
        ProcessModule module,
        long configuredOffset,
        IntPtr address,
        bool usedSignature,
        string? error)
    {
        _resolvedRoots[key] = address;
        _rootResolutions[key] = new PointerRootResolution(
            module.ModuleName,
            configuredOffset,
            address.ToInt64() - module.BaseAddress.ToInt64(),
            usedSignature,
            error);
    }

    private IntPtr ResolveSignatureRoot(ProcessModule module, PointerRootSignature signature)
    {
        var snapshot = GetModuleSnapshot(module);
        var pattern = signature.ParsePattern();
        var matches = FindMatches(snapshot, pattern).Take(2).ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Signature matched {matches.Length}{(matches.Length == 2 ? "+" : string.Empty)} locations.");
        }

        if (signature.DisplacementOffset < 0 ||
            signature.InstructionEndOffset < 0 ||
            signature.DisplacementOffset + sizeof(int) > pattern.Bytes.Length ||
            signature.InstructionEndOffset > pattern.Bytes.Length)
        {
            throw new InvalidOperationException("Signature offsets are outside the pattern.");
        }

        var displacement = BitConverter.ToInt32(snapshot, matches[0] + signature.DisplacementOffset);
        return IntPtr.Add(module.BaseAddress, checked(matches[0] + signature.InstructionEndOffset + displacement));
    }

    private byte[] GetModuleSnapshot(ProcessModule module)
    {
        if (_moduleSnapshots.TryGetValue(module.ModuleName, out var snapshot))
        {
            return snapshot;
        }

        snapshot = new byte[module.ModuleMemorySize];
        var moduleStart = module.BaseAddress.ToInt64();
        var moduleEnd = moduleStart + module.ModuleMemorySize;
        var cursor = moduleStart;

        while (cursor < moduleEnd)
        {
            if (VirtualQueryEx(_handle, new IntPtr(cursor), out var info, (nuint)Marshal.SizeOf<MemoryBasicInformation>()) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not query memory at 0x{cursor:X}.");
            }

            var regionEnd = Math.Min(moduleEnd, info.BaseAddress.ToInt64() + checked((long)info.RegionSize));
            var size = checked((int)(regionEnd - cursor));
            var readable = info.State == MemCommit && (info.Protect & (PageGuard | PageNoAccess)) == 0;
            if (readable)
            {
                var buffer = new byte[size];
                ReadBytes(new IntPtr(cursor), buffer);
                Buffer.BlockCopy(buffer, 0, snapshot, checked((int)(cursor - moduleStart)), size);
            }

            cursor = regionEnd;
        }

        _moduleSnapshots[module.ModuleName] = snapshot;
        return snapshot;
    }

    private static IEnumerable<int> FindMatches(byte[] source, SignaturePattern pattern)
    {
        if (pattern.Bytes.Length == 0)
        {
            yield break;
        }

        var anchor = Array.FindIndex(pattern.Required, required => required);
        if (anchor < 0)
        {
            yield break;
        }

        var index = anchor;
        while (index < source.Length)
        {
            index = Array.IndexOf(source, pattern.Bytes[anchor], index);
            if (index < 0)
            {
                yield break;
            }

            var matchStart = index - anchor;
            var matches = true;
            if (matchStart < 0 || matchStart > source.Length - pattern.Bytes.Length)
            {
                matches = false;
            }
            else
            {
                for (var patternIndex = 0; patternIndex < pattern.Bytes.Length && matches; patternIndex++)
                {
                    matches = !pattern.Required[patternIndex] ||
                        source[matchStart + patternIndex] == pattern.Bytes[patternIndex];
                }
            }

            if (matches)
            {
                yield return matchStart;
            }

            index++;
        }
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
        ReadBytes(address, buffer);
        buffer.CopyTo(destination);
    }

    private void ReadBytes(IntPtr address, byte[] destination)
    {
        if (!ReadProcessMemory(_handle, address, destination, destination.Length, out var bytesRead) || bytesRead != destination.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not read {destination.Length} bytes at 0x{address.ToInt64():X}.");
        }
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
    private static extern nuint VirtualQueryEx(
        IntPtr processHandle,
        IntPtr address,
        out MemoryBasicInformation buffer,
        nuint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryBasicInformation
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public nuint RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }
}

public sealed record PointerRootResolution(
    string ModuleName,
    long ConfiguredOffset,
    long ResolvedOffset,
    bool UsedSignature,
    string? FallbackReason);
