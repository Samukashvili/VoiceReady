using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoiceReady.Core.Configuration;

public static class MemoryMapLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static MemoryMap Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<MemoryMap>(stream, SerializerOptions)
            ?? throw new InvalidOperationException($"Could not deserialize memory map: {path}");
    }
}
