using System.Text.Json;

namespace Tests.TestSupport;

internal static class TestcontainerImages
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly Dictionary<string, TestcontainerImageEntry> Images;

    static TestcontainerImages()
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "testData", "system", "testcontainers.json");
        var jsonContent = File.ReadAllText(jsonPath);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, TestcontainerImageEntry>>(jsonContent, SerializerOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize {jsonPath}");
        Images = parsed;
    }

    public static string GetImageReference(string name)
    {
        if (!Images.TryGetValue(name, out var entry))
        {
            throw new KeyNotFoundException($"Image reference '{name}' not found in testcontainers.json");
        }

        return $"{entry.Image}:{entry.Tag}";
    }

    private sealed class TestcontainerImageEntry
    {
        required public string Image { get; init; }

        required public string Tag { get; init; }
    }
}

