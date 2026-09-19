namespace TestGuardian.Core.Trx.Models;

public sealed record TestRunResult(
    string RunName,
    IReadOnlyList<TestCaseResult> TestCases,
    IReadOnlyList<RunWarning> Warnings);
