namespace VoiceReady.Core.Transcription;

public sealed record TranscriptionResult(string Text, string Language, double LanguageProbability);
