namespace TestGuardian.Core.Input;

/// <summary>
/// Resolves raw CLI inputs (a file path, a directory, or a "directory + filename wildcard"
/// pattern) into concrete .trx file paths. See docs/plans/multi-file-input.md for the
/// reasoning behind the depth-limited directory walk and the wildcard scope cut.
/// </summary>
public static class TrxInputResolver
{
    public static InputResolutionResult Resolve(IEnumerable<string> rawInputs, int maxDepth = 0)
    {
        var resolvedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unresolvedInputs = new List<UnresolvedInput>();

        foreach (var rawInput in rawInputs)
        {
            var matches = ResolveSingleInput(rawInput, maxDepth, out var failureReason);

            if (matches.Count == 0)
            {
                unresolvedInputs.Add(new UnresolvedInput(rawInput, failureReason!));
                continue;
            }

            foreach (var match in matches)
            {
                resolvedPaths.Add(Path.GetFullPath(match));
            }
        }

        var sortedPaths = resolvedPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        return new InputResolutionResult(sortedPaths, unresolvedInputs);
    }

    private static IReadOnlyList<string> ResolveSingleInput(string rawInput, int maxDepth, out string? failureReason)
    {
        if (File.Exists(rawInput))
        {
            failureReason = null;
            return [rawInput];
        }

        if (Directory.Exists(rawInput))
        {
            var found = new List<string>();
            CollectTrxFiles(rawInput, maxDepth, found);

            if (found.Count > 0)
            {
                failureReason = null;
                return found;
            }

            failureReason = maxDepth > 0
                ? $"Der Ordner '{rawInput}' enthält keine .trx-Dateien (durchsucht bis Tiefe {maxDepth})."
                : $"Der Ordner '{rawInput}' enthält keine .trx-Dateien.";
            return [];
        }

        return ResolveAsWildcardPattern(rawInput, out failureReason);
    }

    private static IReadOnlyList<string> ResolveAsWildcardPattern(string rawInput, out string? failureReason)
    {
        var directoryPart = Path.GetDirectoryName(rawInput);
        var filePattern = Path.GetFileName(rawInput);

        if (string.IsNullOrEmpty(filePattern))
        {
            failureReason = $"'{rawInput}' ist weder eine Datei noch ein Ordner noch ein gültiges Suchmuster.";
            return [];
        }

        var searchDirectory = string.IsNullOrEmpty(directoryPart) ? "." : directoryPart;

        if (!Directory.Exists(searchDirectory))
        {
            failureReason = $"Das Verzeichnis '{searchDirectory}' aus dem Suchmuster '{rawInput}' existiert nicht.";
            return [];
        }

        string[] matches;
        try
        {
            matches = Directory.GetFiles(searchDirectory, filePattern, SearchOption.TopDirectoryOnly);
        }
        catch (ArgumentException)
        {
            failureReason = $"'{rawInput}' ist kein gültiges Suchmuster.";
            return [];
        }

        if (matches.Length == 0)
        {
            failureReason = $"Das Suchmuster '{rawInput}' hat keine Datei getroffen.";
            return [];
        }

        failureReason = null;
        return matches;
    }

    private static void CollectTrxFiles(string directory, int depthRemaining, List<string> results)
    {
        results.AddRange(Directory.GetFiles(directory, "*.trx", SearchOption.TopDirectoryOnly));

        if (depthRemaining <= 0)
        {
            return;
        }

        foreach (var subdirectory in Directory.GetDirectories(directory))
        {
            CollectTrxFiles(subdirectory, depthRemaining - 1, results);
        }
    }
}
