using System.Text.Json;
using System.Text.Json.Serialization;
using TestGuardian.Core.Json.Models;

namespace TestGuardian.Core.Json;

/// <summary>
/// Writes exactly once and never overwrites: opens the target with <see cref="FileMode.CreateNew"/>,
/// which itself throws <see cref="IOException"/> if the file already exists — the authoritative
/// guard against data loss, independent of (and stricter than) whatever an earlier CLI-level
/// existence check already caught. See docs/decisions/json-output.md.
/// </summary>
public static class JsonReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Write(string path, JsonReport report)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        JsonSerializer.Serialize(stream, report, Options);
    }
}
