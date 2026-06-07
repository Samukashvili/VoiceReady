using VoiceReady.Core.Configuration;

namespace VoiceReady.Core.Audio;

public sealed class SpeechSegmenter
{
    private readonly AudioSettings _settings;
    private readonly MemoryStream _activeSpeech = new();
    private bool _isSpeaking;
    private int _speechMilliseconds;
    private int _silenceMilliseconds;

    public SpeechSegmenter(AudioSettings settings)
    {
        _settings = settings;
    }

    public double CurrentDecibels { get; private set; } = -96;

    public bool IsSpeaking => _isSpeaking;

    public SpeechSegment? Process(PcmAudioFrame frame)
    {
        var decibels = CalculateDecibels(frame.Data);
        CurrentDecibels = decibels;
        var frameMilliseconds = _settings.FrameMilliseconds;

        if (!_isSpeaking && decibels >= _settings.SpeechStartDb)
        {
            _isSpeaking = true;
            _speechMilliseconds = 0;
            _silenceMilliseconds = 0;
            _activeSpeech.SetLength(0);
        }

        if (!_isSpeaking)
        {
            return null;
        }

        _activeSpeech.Write(frame.Data);
        _speechMilliseconds += frameMilliseconds;

        if (decibels <= _settings.SpeechEndDb)
        {
            _silenceMilliseconds += frameMilliseconds;
        }
        else
        {
            _silenceMilliseconds = 0;
        }

        var hitMaxLength = _speechMilliseconds >= _settings.MaximumSegmentMilliseconds;
        var hitEndSilence = _silenceMilliseconds >= _settings.TrailingSilenceMilliseconds;
        if (!hitMaxLength && !hitEndSilence)
        {
            return null;
        }

        var pcm = _activeSpeech.ToArray();
        var valid = _speechMilliseconds >= _settings.MinimumSpeechMilliseconds;
        Reset();

        return valid
            ? new SpeechSegment(pcm, frame.SampleRate, frame.Channels, frame.BitsPerSample)
            : null;
    }

    private void Reset()
    {
        _isSpeaking = false;
        _speechMilliseconds = 0;
        _silenceMilliseconds = 0;
        _activeSpeech.SetLength(0);
    }

    public static double CalculateDecibels(byte[] pcm16)
    {
        if (pcm16.Length < 2)
        {
            return -96;
        }

        double sumSquares = 0;
        var sampleCount = pcm16.Length / 2;
        for (var i = 0; i < pcm16.Length - 1; i += 2)
        {
            var sample = BitConverter.ToInt16(pcm16, i) / 32768.0;
            sumSquares += sample * sample;
        }

        var rms = Math.Sqrt(sumSquares / sampleCount);
        return rms <= 0.000001 ? -96 : 20 * Math.Log10(rms);
    }
}
