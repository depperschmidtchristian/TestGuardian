namespace TestGuardian.Core.Trx;

public sealed record TestRunResult(
    string RunName,
    IReadOnlyList<TestCaseResult> TestCases,
    IReadOnlyList<RunWarning> Warnings);
