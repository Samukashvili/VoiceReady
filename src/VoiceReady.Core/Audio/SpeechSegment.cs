namespace VoiceReady.Core.Audio;

public sealed record SpeechSegment(byte[] PcmData, int SampleRate, short Channels, short BitsPerSample);
