using System.Globalization;
using System.Text.Json;

namespace VoiceReady.Core.Configuration;

public sealed class VoiceReadySettings
{
    public AudioSettings Audio { get; init; } = new();

    public TranscriptionSettings Transcription { get; init; } = new();

    public InputSettings Input { get; init; } = new();
}

public sealed class AudioSettings
{
    public int DeviceNumber { get; init; }

    public int SampleRate { get; init; } = 16000;

    public short Channels { get; init; } = 1;

    public int FrameMilliseconds { get; init; } = 30;

    public double SpeechStartDb { get; init; } = -35;

    public double SpeechEndDb { get; init; } = -42;

    public int MinimumSpeechMilliseconds { get; init; } = 350;

    public int TrailingSilenceMilliseconds { get; init; } = 550;

    public int MaximumSegmentMilliseconds { get; init; } = 7000;
}

public sealed class TranscriptionSettings
{
    public string PythonExe { get; init; } = "python";

    public string WorkerScript { get; init; } = "tools/faster-whisper/transcribe.py";

    public string ModelPath { get; init; } = "tools/faster-whisper/models/base.en";

    public string Language { get; init; } = "en";

    public string Device { get; init; } = "cpu";

    public string ComputeType { get; init; } = "int8";
}

public sealed class InputSettings
{
    public CommandMenuOpenInput CommandMenuOpen { get; init; } = new();

    public int KeyHoldMilliseconds { get; init; } = 35;

    public int BetweenKeysMilliseconds { get; init; } = 80;

    public int StateTransitionTimeoutMilliseconds { get; init; } = 700;

    public string CloseMenuScanCode { get; init; } = "01";

    public ushort CloseMenuScanCodeValue => ushort.Parse(
        CloseMenuScanCode.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? CloseMenuScanCode[2..] : CloseMenuScanCode,
        NumberStyles.HexNumber,
        CultureInfo.InvariantCulture);
}

public sealed class CommandMenuOpenInput
{
    public string Kind { get; init; } = "MouseMiddle";

    public string ScanCode { get; init; } = string.Empty;
}

public static class VoiceReadySettingsLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static VoiceReadySettings Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<VoiceReadySettings>(stream, SerializerOptions)
            ?? throw new InvalidOperationException($"Could not deserialize VoiceReady settings: {path}");
    }
}
