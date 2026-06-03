namespace VoiceReady.Core.Memory;

public sealed record PointerReadResult(
    PointerPath Pointer,
    bool Success,
    IntPtr? ResolvedAddress,
    int? Value,
    string? Error)
{
    public static PointerReadResult Failed(PointerPath pointer, string error)
    {
        return new PointerReadResult(pointer, false, null, null, error);
    }

    public static PointerReadResult Succeeded(PointerPath pointer, IntPtr resolvedAddress, int value)
    {
        return new PointerReadResult(pointer, true, resolvedAddress, value, null);
    }
}
