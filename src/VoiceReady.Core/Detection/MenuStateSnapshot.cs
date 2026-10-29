using VoiceReady.Core.Memory;

namespace VoiceReady.Core.Detection;

public sealed record MenuStateSnapshot(
    int? VotedValue,
    double Confidence,
    int SuccessfulReads,
    int FailedReads,
    IReadOnlyList<PointerReadResult> Reads,
    bool IsDirect = false,
    string? Detail = null)
{
    public bool IsReliable => VotedValue.HasValue && (IsDirect || (Confidence >= 0.6 && SuccessfulReads >= 3));

    public string Source => IsDirect ? "reflection" : "pointers";
}
