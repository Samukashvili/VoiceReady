using System.Diagnostics;
using System.Text.Json;
using VoiceReady.Core.Configuration;

namespace VoiceReady.Core.Transcription;

public sealed class FasterWhisperTranscriber
{
    private readonly TranscriptionSettings _settings;
    private readonly string _repoRoot;
    private Process? _worker;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private StreamReader? _stderr;

    public FasterWhisperTranscriber(TranscriptionSettings settings, string repoRoot)
    {
        _settings = settings;
        _repoRoot = repoRoot;
    }

    public async Task<TranscriptionResult?> TranscribeAsync(string wavPath, CancellationToken cancellationToken)
    {
        await EnsureWorkerAsync(cancellationToken);
        _stdin!.WriteLine(wavPath);
        await _stdin.FlushAsync(cancellationToken);

        var stdout = await _stdout!.ReadLineAsync(cancellationToken)
            ?? throw new InvalidOperationException("faster-whisper worker exited without returning a result.");

        return ParseResult(stdout);
    }

    private async Task EnsureWorkerAsync(CancellationToken cancellationToken)
    {
        if (_worker is { HasExited: false })
        {
            return;
        }

        var scriptPath = ResolvePath(_settings.WorkerScript);
        var modelPath = ResolvePath(_settings.ModelPath);

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("Faster-whisper worker script was not found.", scriptPath);
        }

        if (!Directory.Exists(modelPath) && !File.Exists(modelPath))
        {
            throw new DirectoryNotFoundException($"Faster-whisper model path was not found: {modelPath}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _settings.PythonExe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = _repoRoot
        };

        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("--model");
        startInfo.ArgumentList.Add(modelPath);
        startInfo.ArgumentList.Add("--language");
        startInfo.ArgumentList.Add(_settings.Language);
        startInfo.ArgumentList.Add("--device");
        startInfo.ArgumentList.Add(_settings.Device);
        startInfo.ArgumentList.Add("--compute-type");
        startInfo.ArgumentList.Add(_settings.ComputeType);
        startInfo.ArgumentList.Add("--server");

        startInfo.RedirectStandardInput = true;

        _worker = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start faster-whisper worker.");
        try
        {
            _worker.PriorityClass = ProcessPriorityClass.BelowNormal;
        }
        catch
        {
            // Priority changes can fail under restricted process permissions; transcription still works without it.
        }

        _stdin = _worker.StandardInput;
        _stdout = _worker.StandardOutput;
        _stderr = _worker.StandardError;

        var readyLine = await _stdout.ReadLineAsync(cancellationToken)
            ?? throw new InvalidOperationException("faster-whisper worker did not report readiness.");
        using var readyDocument = JsonDocument.Parse(readyLine);
        if (!readyDocument.RootElement.TryGetProperty("ready", out var readyElement) || !readyElement.GetBoolean())
        {
            throw new InvalidOperationException($"Unexpected faster-whisper startup response: {readyLine}");
        }
    }

    private static TranscriptionResult? ParseResult(string stdout)
    {
        using var document = JsonDocument.Parse(stdout);
        var root = document.RootElement;
        if (root.TryGetProperty("error", out var errorElement))
        {
            throw new InvalidOperationException(errorElement.GetString());
        }

        var text = root.GetProperty("text").GetString() ?? string.Empty;
        var language = root.TryGetProperty("language", out var languageElement)
            ? languageElement.GetString() ?? string.Empty
            : string.Empty;
        var probability = root.TryGetProperty("languageProbability", out var probabilityElement)
            ? probabilityElement.GetDouble()
            : 0;

        return string.IsNullOrWhiteSpace(text)
            ? null
            : new TranscriptionResult(text, language, probability);
    }

    private string ResolvePath(string path)
    {
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(_repoRoot, path));
    }
}
