using System.Text.Json;
using VoiceReady.Core.Audio;
using VoiceReady.Core.Configuration;
using Vosk;

namespace VoiceReady.Core.Transcription;

public sealed class VoskTranscriber : IDisposable
{
    private static readonly string[] RequiredModelFiles =
    [
        "conf\\model.conf",
        "am\\final.mdl",
        "graph\\HCLr.fst",
        "graph\\Gr.fst",
        "graph\\phones\\word_boundary.int",
        "ivector\\final.ie"
    ];

    private readonly Model _model;
    private readonly VoskRecognizer _recognizer;

    public VoskTranscriber(VoskSettings settings, string repoRoot, int sampleRate)
    {
        var modelPath = Path.IsPathRooted(settings.ModelPath)
            ? settings.ModelPath
            : Path.GetFullPath(Path.Combine(repoRoot, settings.ModelPath));

        if (!Directory.Exists(modelPath))
        {
            throw new DirectoryNotFoundException(
                $"Vosk model was not found: {modelPath}. Download or clone the full repository, including tools/vosk/models.");
        }

        ValidateModelFiles(modelPath);
        ValidateNativeSafeModelPath(modelPath);
        Vosk.Vosk.SetLogLevel(-2);
        _model = new Model(modelPath);
        var grammarJson = JsonSerializer.Serialize(
            VoiceCommandGrammar.Build(settings.AdditionalGrammarPhrases, settings.IncludeUnknownWords));
        _recognizer = new VoskRecognizer(_model, sampleRate, grammarJson);
        _recognizer.SetMaxAlternatives(settings.MaximumAlternatives);
    }

    private static void ValidateModelFiles(string modelPath)
    {
        foreach (var relativePath in RequiredModelFiles)
        {
            var path = Path.Combine(modelPath, relativePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Vosk model is incomplete. Missing required file: {path}. Download or clone the full repository again.");
            }

            if (new FileInfo(path).Length == 0)
            {
                throw new InvalidOperationException($"Vosk model file is empty: {path}");
            }
        }
    }

    private static void ValidateNativeSafeModelPath(string modelPath)
    {
        if (IsAscii(modelPath))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Vosk cannot load the model from a path with non-English characters: {modelPath}. Move the whole VoiceReady folder to a simple path such as C:\\VoiceReady and run it again.");
    }

    private static bool IsAscii(string value) => value.All(character => character <= 127);

    public TranscriptionResult? Transcribe(SpeechSegment segment)
    {
        _recognizer.AcceptWaveform(segment.PcmData, segment.PcmData.Length);

        using var document = JsonDocument.Parse(_recognizer.FinalResult());
        _recognizer.Reset();
        var text = ExtractText(document.RootElement);
        return string.IsNullOrWhiteSpace(text)
            ? null
            : new TranscriptionResult(text, "en", 1);
    }

    public void Dispose()
    {
        _recognizer.Dispose();
        _model.Dispose();
    }

    private static string ExtractText(JsonElement root)
    {
        if (root.TryGetProperty("text", out var textElement))
        {
            return textElement.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("alternatives", out var alternatives) &&
            alternatives.ValueKind == JsonValueKind.Array &&
            alternatives.GetArrayLength() > 0 &&
            alternatives[0].TryGetProperty("text", out textElement))
        {
            return textElement.GetString() ?? string.Empty;
        }

        return string.Empty;
    }
}
