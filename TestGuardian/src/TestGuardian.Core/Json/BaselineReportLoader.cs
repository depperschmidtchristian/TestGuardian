using System.Text.Json;
using TestGuardian.Core.Json.Models;

namespace TestGuardian.Core.Json;

/// <summary>
/// Reading an existing file is safe (unlike writing — see <see cref="JsonReportWriter"/>), so this
/// is a single load step: no separate "can we even read this" pre-check needed. Uses the exact same
/// <see cref="JsonSerializerOptions"/> as <see cref="JsonReportWriter"/> so read and write always agree.
/// </summary>
public static class BaselineReportLoader
{
    public static BaselineLoadResult Load(string path)
    {
        if (!File.Exists(path))
        {
            return new BaselineLoadResult.Failure($"Die Baseline-Datei '{path}' existiert nicht.");
        }

        try
        {
            var report = JsonSerializer.Deserialize<JsonReport>(File.ReadAllText(path), JsonReportWriter.Options);
            return report is null
                ? new BaselineLoadResult.Failure($"Die Baseline-Datei '{path}' enthält kein gültiges JSON-Objekt.")
                : new BaselineLoadResult.Success(report);
        }
        catch (JsonException ex)
        {
            return new BaselineLoadResult.Failure(
                $"Die Baseline-Datei '{path}' ist kein gültiger TestGuardian-JSON-Bericht: {ex.Message}");
        }
    }
}
