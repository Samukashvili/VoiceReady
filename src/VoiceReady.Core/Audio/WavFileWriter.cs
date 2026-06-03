using System.Text;

namespace VoiceReady.Core.Audio;

public static class WavFileWriter
{
    public static void Write(string path, SpeechSegment segment)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII);

        var byteRate = segment.SampleRate * segment.Channels * segment.BitsPerSample / 8;
        var blockAlign = (short)(segment.Channels * segment.BitsPerSample / 8);
        var dataSize = segment.PcmData.Length;

        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(segment.Channels);
        writer.Write(segment.SampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(segment.BitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataSize);
        writer.Write(segment.PcmData);
    }
}
