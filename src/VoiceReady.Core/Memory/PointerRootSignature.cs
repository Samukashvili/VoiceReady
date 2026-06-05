using System.Globalization;

namespace VoiceReady.Core.Memory;

public sealed record PointerRootSignature(
    string Pattern,
    int DisplacementOffset,
    int InstructionEndOffset)
{
    public SignaturePattern ParsePattern() => SignaturePattern.Parse(Pattern);
}

public sealed record SignaturePattern(byte[] Bytes, bool[] Required)
{
    public static SignaturePattern Parse(string pattern)
    {
        var tokens = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var bytes = new byte[tokens.Length];
        var required = new bool[tokens.Length];

        for (var index = 0; index < tokens.Length; index++)
        {
            if (tokens[index] is "?" or "??")
            {
                continue;
            }

            bytes[index] = byte.Parse(tokens[index], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            required[index] = true;
        }

        return new SignaturePattern(bytes, required);
    }
}
