using System.Text.Json;

namespace VoiceReady.Core.Configuration;

public static class CommandMenuMapLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static CommandMenuMap Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<CommandMenuMap>(stream, SerializerOptions)
            ?? throw new InvalidOperationException($"Could not deserialize command menu map: {path}");
    }
}
