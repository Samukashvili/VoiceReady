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
        modelPath = EnsureNativeSafeModelPath(modelPath);
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

    private static string EnsureNativeSafeModelPath(string modelPath)
    {
        if (IsAscii(modelPath))
        {
            return modelPath;
        }

        var cacheRoot = GetAsciiWritableCacheRoot();
        var cachePath = Path.Combine(cacheRoot, "vosk-model-small-en-us-0.15");
        MirrorDirectory(modelPath, cachePath);
        ValidateModelFiles(cachePath);
        return cachePath;
    }

    private static string GetAsciiWritableCacheRoot()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "VoiceReady", "VoskCache"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VoiceReady", "VoskCache"),
            Path.Combine(Path.GetTempPath(), "VoiceReady", "VoskCache")
        };

        foreach (var candidate in candidates.Where(path => !string.IsNullOrWhiteSpace(path) && IsAscii(path)))
        {
            try
            {
                Directory.CreateDirectory(candidate);
                var probe = Path.Combine(candidate, ".write-test");
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
                return candidate;
            }
            catch
            {
                // Try the next candidate.
            }
        }

        throw new InvalidOperationException(
            "Vosk cannot load the model because VoiceReady is stored in a path with non-English characters, and no ASCII-only writable cache folder was available. Move VoiceReady to a simple path such as C:\\VoiceReady and try again.");
    }

    private static void MirrorDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var sourcePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourcePath);
            var targetPath = Path.Combine(targetDirectory, relativePath);
            var targetParent = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetParent))
            {
                Directory.CreateDirectory(targetParent);
            }

            if (File.Exists(targetPath) && new FileInfo(targetPath).Length == new FileInfo(sourcePath).Length)
            {
                continue;
            }

            File.Copy(sourcePath, targetPath, overwrite: true);
        }
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
