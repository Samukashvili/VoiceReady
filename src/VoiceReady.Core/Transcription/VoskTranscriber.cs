using System.Text.Json;
using VoiceReady.Core.Audio;
using VoiceReady.Core.Configuration;
using Vosk;

namespace VoiceReady.Core.Transcription;

public sealed class VoskTranscriber : IDisposable
{
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
                $"Vosk model was not found: {modelPath}. Run install-dependencies.bat first.");
        }

        Vosk.Vosk.SetLogLevel(-2);
        _model = new Model(modelPath);
        var grammarJson = JsonSerializer.Serialize(
            VoiceCommandGrammar.Build(settings.AdditionalGrammarPhrases, settings.IncludeUnknownWords));
        _recognizer = new VoskRecognizer(_model, sampleRate, grammarJson);
        _recognizer.SetMaxAlternatives(settings.MaximumAlternatives);
    }

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
