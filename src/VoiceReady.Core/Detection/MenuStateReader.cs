using VoiceReady.Core.Memory;

namespace VoiceReady.Core.Detection;

public sealed class MenuStateReader
{
    private readonly ProcessMemoryReader _memoryReader;
    private readonly IReadOnlyList<PointerPath> _pointers;

    public MenuStateReader(ProcessMemoryReader memoryReader, IReadOnlyList<PointerPath> pointers)
    {
        _memoryReader = memoryReader;
        _pointers = pointers;
    }

    public MenuStateSnapshot Read()
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
