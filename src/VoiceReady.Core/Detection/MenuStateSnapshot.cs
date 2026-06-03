using VoiceReady.Core.Memory;

namespace VoiceReady.Core.Detection;

public sealed record MenuStateSnapshot(
    int? VotedValue,
    double Confidence,
    int SuccessfulReads,
    int FailedReads,
    IReadOnlyList<PointerReadResult> Reads)
{
    public bool IsReliable => VotedValue.HasValue && Confidence >= 0.6 && SuccessfulReads >= 3;
}
