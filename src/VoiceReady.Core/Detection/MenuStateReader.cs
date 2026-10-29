using VoiceReady.Core.Memory;

namespace VoiceReady.Core.Detection;

public sealed class MenuStateReader
{
    private readonly ProcessMemoryReader _memoryReader;
    private readonly IReadOnlyList<PointerPath> _pointers;
    private readonly Func<DirectStateRead>? _directSource;

    public MenuStateReader(
        ProcessMemoryReader memoryReader,
        IReadOnlyList<PointerPath> pointers,
        Func<DirectStateRead>? directSource = null)
    {
        _memoryReader = memoryReader;
        _pointers = pointers;
        _directSource = directSource;
    }

    /// <summary>Why the last read fell back to pointer paths, or null when the direct source was used.</summary>
    public string? FallbackReason { get; private set; }

    public MenuStateSnapshot Read()
    {
        if (_directSource is not null)
        {
            var direct = _directSource();
            if (direct.Available)
            {
                FallbackReason = null;
                return new MenuStateSnapshot(direct.Value, direct.Value.HasValue ? 1 : 0, direct.Value.HasValue ? 1 : 0, direct.Value.HasValue ? 0 : 1, [], true, direct.Detail);
            }

            FallbackReason = direct.Detail;
        }

        return ReadPointers();
    }

    private MenuStateSnapshot ReadPointers()
    {
        var reads = _pointers.Select(_memoryReader.ReadInt32).ToArray();
        var successful = reads.Where(read => read.Success && read.Value.HasValue).ToArray();
        var failedCount = reads.Length - successful.Length;

        if (successful.Length == 0)
        {
            return new MenuStateSnapshot(null, 0, 0, failedCount, reads);
        }

        var vote = successful
            .GroupBy(read => read.Value!.Value)
            .Select(group => new { Value = group.Key, Count = group.Count() })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Value)
            .First();

        var confidence = (double)vote.Count / successful.Length;
        return new MenuStateSnapshot(vote.Value, confidence, successful.Length, failedCount, reads);
    }
}
