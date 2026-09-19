namespace TestGuardian.Core.Trx.Models;

/// <summary>
/// A single classified test result. <see cref="RawOutcome"/> preserves the original
/// vstest outcome string even when <see cref="Outcome"/> falls back to <see cref="TestOutcome.Other"/>,
/// so unrecognized outcomes stay visible instead of disappearing into a generic bucket.
/// </summary>
public sealed record TestCaseResult(
    string AssemblyName,
    string TestName,
    TestOutcome Outcome,
    string RawOutcome,
    string? ErrorMessage);
