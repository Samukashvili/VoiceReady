namespace VoiceReady.Core.Audio;

public sealed record PcmAudioFrame(byte[] Data, int SampleRate, short Channels, short BitsPerSample);
