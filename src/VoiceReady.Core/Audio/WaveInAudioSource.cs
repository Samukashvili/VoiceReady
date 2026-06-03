using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using VoiceReady.Core.Configuration;

namespace VoiceReady.Core.Audio;

public sealed class WaveInAudioSource : IDisposable
{
    private const int WaveMapper = -1;
    private const int MmWimData = 0x3C0;
    private const int CallbackFunction = 0x00030000;

    private readonly AudioSettings _settings;
    private readonly ConcurrentQueue<PcmAudioFrame> _frames = new();
    private readonly WaveInProc _callback;
    private readonly List<IntPtr> _headerPointers = [];
    private readonly List<IntPtr> _bufferPointers = [];
    private IntPtr _handle;
    private bool _disposed;

    public WaveInAudioSource(AudioSettings settings)
    {
        _settings = settings;
        _callback = OnWaveIn;
    }

    public void Start()
    {
        var format = new WAVEFORMATEX
        {
            wFormatTag = 1,
            nChannels = _settings.Channels,
            nSamplesPerSec = _settings.SampleRate,
            wBitsPerSample = 16,
            nBlockAlign = (short)(_settings.Channels * 2),
            nAvgBytesPerSec = _settings.SampleRate * _settings.Channels * 2,
            cbSize = 0
        };

        var deviceId = _settings.DeviceNumber < 0 ? WaveMapper : _settings.DeviceNumber;
        Check(waveInOpen(out _handle, deviceId, ref format, _callback, IntPtr.Zero, CallbackFunction), "waveInOpen");

        var bufferSize = _settings.SampleRate * _settings.Channels * 2 * _settings.FrameMilliseconds / 1000;
        for (var i = 0; i < 6; i++)
        {
            AddBuffer(bufferSize);
        }

        Check(waveInStart(_handle), "waveInStart");
    }

    public bool TryRead(out PcmAudioFrame? frame)
    {
        if (_frames.TryDequeue(out var result))
        {
            frame = result;
            return true;
        }

        frame = null;
        return false;
    }

    private void AddBuffer(int bufferSize)
    {
        var bufferPointer = Marshal.AllocHGlobal(bufferSize);
        var header = new WAVEHDR
        {
            lpData = bufferPointer,
            dwBufferLength = bufferSize
        };
        var headerPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WAVEHDR>());
        Marshal.StructureToPtr(header, headerPointer, false);

        Check(waveInPrepareHeader(_handle, headerPointer, Marshal.SizeOf<WAVEHDR>()), "waveInPrepareHeader");
        Check(waveInAddBuffer(_handle, headerPointer, Marshal.SizeOf<WAVEHDR>()), "waveInAddBuffer");

        _bufferPointers.Add(bufferPointer);
        _headerPointers.Add(headerPointer);
    }

    private void OnWaveIn(IntPtr hwi, int message, IntPtr instance, IntPtr param1, IntPtr param2)
    {
        if (message != MmWimData || _disposed)
        {
            return;
        }

        var header = Marshal.PtrToStructure<WAVEHDR>(param1);
        if (header.dwBytesRecorded > 0)
        {
            var data = new byte[header.dwBytesRecorded];
            Marshal.Copy(header.lpData, data, 0, data.Length);
            _frames.Enqueue(new PcmAudioFrame(data, _settings.SampleRate, _settings.Channels, 16));
        }

        if (!_disposed)
        {
            waveInAddBuffer(_handle, param1, Marshal.SizeOf<WAVEHDR>());
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_handle != IntPtr.Zero)
        {
            waveInStop(_handle);
            waveInReset(_handle);

            foreach (var headerPointer in _headerPointers)
            {
                waveInUnprepareHeader(_handle, headerPointer, Marshal.SizeOf<WAVEHDR>());
                Marshal.FreeHGlobal(headerPointer);
            }

            foreach (var bufferPointer in _bufferPointers)
            {
                Marshal.FreeHGlobal(bufferPointer);
            }

            waveInClose(_handle);
        }
    }

    private static void Check(int result, string operation)
    {
        if (result != 0)
        {
            throw new Win32Exception(result, $"{operation} failed with MMRESULT {result}.");
        }
    }

    private delegate void WaveInProc(IntPtr hwi, int uMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEFORMATEX
    {
        public short wFormatTag;
        public short nChannels;
        public int nSamplesPerSec;
        public int nAvgBytesPerSec;
        public short nBlockAlign;
        public short wBitsPerSample;
        public short cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEHDR
    {
        public IntPtr lpData;
        public int dwBufferLength;
        public int dwBytesRecorded;
        public IntPtr dwUser;
        public int dwFlags;
        public int dwLoops;
        public IntPtr lpNext;
        public IntPtr reserved;
    }

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern int waveInOpen(out IntPtr hWaveIn, int uDeviceID, ref WAVEFORMATEX lpFormat, WaveInProc dwCallback, IntPtr dwInstance, int dwFlags);

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern int waveInPrepareHeader(IntPtr hWaveIn, IntPtr lpWaveInHdr, int uSize);

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern int waveInAddBuffer(IntPtr hWaveIn, IntPtr lpWaveInHdr, int uSize);

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern int waveInStart(IntPtr hWaveIn);

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern int waveInStop(IntPtr hWaveIn);

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern int waveInReset(IntPtr hWaveIn);

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern int waveInUnprepareHeader(IntPtr hWaveIn, IntPtr lpWaveInHdr, int uSize);

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern int waveInClose(IntPtr hWaveIn);
}
