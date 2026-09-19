namespace TestGuardian.Core.Trx.Models;

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
    /// Tests whose body was actually started, regardless of how clear the verdict was.
    /// Excludes <see cref="TestOutcome.LoadError"/> (assembly never loaded) and
    /// <see cref="TestOutcome.Skipped"/> (test explicitly did not execute), but still
    /// includes <see cref="TestOutcome.Other"/> (e.g. Inconclusive, Timeout) since those
    /// outcomes mean the body ran, just without a clean pass/fail result.
    /// </summary>
    public int Attempted => Passed + Failed + Other;

    /// <summary>
    /// Tests that ran and reached an unambiguous verdict. Stricter than
    /// <see cref="Attempted"/>: excludes <see cref="TestOutcome.Other"/> as well, since an
    /// unrecognized/ambiguous outcome gives no real confidence that anything was verified.
    /// </summary>
    public int CorrectlyExecuted => Passed + Failed;
}
