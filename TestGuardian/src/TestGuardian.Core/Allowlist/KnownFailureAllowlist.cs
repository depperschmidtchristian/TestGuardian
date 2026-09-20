namespace TestGuardian.Core.Allowlist;

/// <summary>
/// Plain text, one test name per line — <c>#</c>-comments and blank lines ignored. Matches only by
/// test name, not by assembly: in real <c>vstest.console.exe</c> runs the assembly is the raw
/// <c>storage</c> path (machine-specific), which would make a hand-maintained allowlist file
/// unusable across machines/build configurations. See docs/decisions/known-failures-allowlist.md.
/// </summary>
public sealed class KnownFailureAllowlist
{
    public static readonly KnownFailureAllowlist Empty = new([]);

    private readonly HashSet<string> _testNames;

    private KnownFailureAllowlist(HashSet<string> testNames) => _testNames = testNames;

    public bool Contains(string testName) => _testNames.Contains(testName);

    public static KnownFailureAllowlist Parse(IEnumerable<string> lines) =>
        new(lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal));
}
