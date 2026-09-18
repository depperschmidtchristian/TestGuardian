namespace TestGuardian.Core.Trx;

/// <summary>
/// Per-assembly view over a set of test cases. Holds only the raw list; every count is a
/// computed property so the numbers can never drift out of sync with the underlying data.
/// </summary>
public sealed record AssemblySummary(string AssemblyName, IReadOnlyList<TestCaseResult> TestCases)
{
    public int Passed => TestCases.Count(t => t.Outcome == TestOutcome.Passed);
    public int Failed => TestCases.Count(t => t.Outcome == TestOutcome.Failed);
    public int LoadError => TestCases.Count(t => t.Outcome == TestOutcome.LoadError);
    public int Skipped => TestCases.Count(t => t.Outcome == TestOutcome.Skipped);
    public int Other => TestCases.Count(t => t.Outcome == TestOutcome.Other);
    public int Total => TestCases.Count;

    /// <summary>
    /// Tests whose body actually ran and produced a real verdict. Excludes
    /// <see cref="TestOutcome.LoadError"/> (assembly never loaded) and
    /// <see cref="TestOutcome.Skipped"/> (test explicitly did not execute).
    /// </summary>
    public int Executed => Passed + Failed + Other;
}
