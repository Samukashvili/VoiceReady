using System.Globalization;

namespace VoiceReady.Core.Memory;

public sealed record PointerPath(
    string Name,
    string ModuleName,
    long BaseOffset,
    IReadOnlyList<long> Offsets,
    PointerRootSignature? RootSignature = null,
    PointerOffsetOrder OffsetOrder = PointerOffsetOrder.CheatEnginePointerScanner)
{
    public static PointerPath FromHex(
        string name,
        string moduleName,
        string baseOffset,
        IEnumerable<string> offsets,
        PointerRootSignature? rootSignature = null,
        PointerOffsetOrder offsetOrder = PointerOffsetOrder.CheatEnginePointerScanner)
    {
        return new PointerPath(
            name,
            moduleName,
            ParseHex(baseOffset),
            offsets.Select(ParseHex).ToArray(),
            rootSignature,
            offsetOrder);
    }

    private static long ParseHex(string value)
    {
        var normalized = value.Trim();

        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }

        return long.Parse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
