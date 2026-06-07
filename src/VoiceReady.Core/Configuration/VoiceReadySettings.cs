using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoiceReady.Core.Configuration;

public sealed class VoiceReadySettings
{
    public AudioSettings Audio { get; init; } = new();

    public VoskSettings Vosk { get; init; } = new();

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

public sealed class VoskSettings
{
    public string ModelPath { get; init; } = "tools/vosk/models/vosk-model-small-en-us-0.15";

    public int MaximumAlternatives { get; init; }

    public bool IncludeUnknownWords { get; init; } = true;

    public List<string> AdditionalGrammarPhrases { get; init; } = [];
}

public sealed class InputSettings
{
    public InputBinding CommandMenuOpen { get; init; } = InputBinding.MouseMiddle();

    public Dictionary<string, InputBinding> CommandKeys { get; init; } = CreateDefaultCommandKeys();

    public int KeyHoldMilliseconds { get; init; } = 35;

    public int BetweenKeysMilliseconds { get; init; } = 80;

    public int StateTransitionTimeoutMilliseconds { get; init; } = 700;

    public string CloseMenuScanCode { get; init; } = "01";

    public int TeamSelectionWheelDelta { get; init; } = -120;

    public int TeamSelectionMaximumScrolls { get; init; } = 5;

    public ushort CloseMenuScanCodeValue => ushort.Parse(
        CloseMenuScanCode.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? CloseMenuScanCode[2..] : CloseMenuScanCode,
        NumberStyles.HexNumber,
        CultureInfo.InvariantCulture);

    public InputBinding GetCommandKey(string key)
    {
        return CommandKeys.TryGetValue(key, out var binding)
            ? binding
            : GetDefaultCommandKey(key);
    }

    public static Dictionary<string, InputBinding> CreateDefaultCommandKeys() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["1"] = InputBinding.Keyboard("02", "1"),
        ["2"] = InputBinding.Keyboard("03", "2"),
        ["3"] = InputBinding.Keyboard("04", "3"),
        ["4"] = InputBinding.Keyboard("05", "4"),
        ["5"] = InputBinding.Keyboard("06", "5"),
        ["6"] = InputBinding.Keyboard("07", "6"),
        ["7"] = InputBinding.Keyboard("08", "7"),
        ["8"] = InputBinding.Keyboard("09", "8"),
        ["9"] = InputBinding.Keyboard("0A", "9"),
        ["0"] = InputBinding.Keyboard("0B", "0")
    };

    public static InputBinding GetDefaultCommandKey(string key)
    {
        var defaults = CreateDefaultCommandKeys();
        return defaults.TryGetValue(key, out var binding)
            ? binding
            : throw new NotSupportedException($"Unsupported command key: {key}");
    }
}

public sealed class InputBinding
{
    public string Kind { get; init; } = "MouseMiddle";

    public string ScanCode { get; init; } = string.Empty;

    public string DisplayName { get; init; } = "Middle Mouse";

    [JsonIgnore]
    public ushort ScanCodeValue => string.IsNullOrWhiteSpace(ScanCode)
        ? (ushort)0
        : ushort.Parse(
            ScanCode.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? ScanCode[2..] : ScanCode,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture);

    public static InputBinding Keyboard(string scanCode, string displayName) => new()
    {
        Kind = "Keyboard",
        ScanCode = scanCode,
        DisplayName = displayName
    };

    public static InputBinding MouseMiddle() => new()
    {
        Kind = "MouseMiddle",
        DisplayName = "Middle Mouse"
    };

    public static InputBinding MouseButton(string kind, string displayName) => new()
    {
        Kind = kind,
        DisplayName = displayName
    };
}

public static class VoiceReadySettingsLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static VoiceReadySettings Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<VoiceReadySettings>(stream, SerializerOptions)
            ?? throw new InvalidOperationException($"Could not deserialize VoiceReady settings: {path}");
    }

    public static void Save(string path, VoiceReadySettings settings)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, settings, SerializerOptions);
    }
}
