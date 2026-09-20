namespace TestGuardian.Console;

/// <summary>
/// Fail-fast CLI-level check for <c>--to-json &lt;path&gt;</c>, run before anything else happens
/// (before input resolution, before any .trx file is read). Never opens the target file itself —
/// only checks its existence and its directory's — so it cannot be the thing that damages an
/// existing file. The actual write later on (<see cref="TestGuardian.Core.Json.JsonReportWriter"/>)
/// is the only place the target file is ever opened, and only with a mode that itself refuses to overwrite.
/// See docs/decisions/json-output.md.
/// </summary>
public static class JsonOutputPathValidator
{
    /// <summary>
    /// Used when <c>--to-json</c> is given without an explicit path: counts up from 1 until it
    /// finds a name not already taken in <paramref name="directory"/> — never touches any file,
    /// same "existence check only" rule as <see cref="EnsureCanCreate"/>.
    /// </summary>
    public static string GenerateDefaultPath(string directory)
    {
        for (var n = 1; ; n++)
        {
            var candidate = Path.Combine(directory, $"testguardian_report_{n}.json");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    public static void EnsureCanCreate(string path)
    {
        if (File.Exists(path))
        {
            throw new ArgumentException(
                $"Die Datei '{path}' existiert bereits. Um Datenverlust zu vermeiden, wird sie nicht überschrieben.");
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            throw new ArgumentException($"Das Verzeichnis '{directory}' für den JSON-Zielpfad '{path}' existiert nicht.");
        }
    }
}
