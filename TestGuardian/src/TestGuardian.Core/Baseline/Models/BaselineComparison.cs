using TestGuardian.Core.Trx.Models;

namespace TestGuardian.Core.Baseline.Models;

public sealed record TestOutcomeEntry(string AssemblyName, string TestName, TestOutcome Outcome);

/// <summary>
/// Answers "war dieser Test gestern schon rot?" for the tests that are red (Failed/LoadError)
/// right now: split into ones that weren't red in the baseline (<see cref="NewlyFailed"/>) and
/// ones that already were (<see cref="AlreadyFailingInBaseline"/>), plus tests that were red in
/// the baseline but aren't anymore (<see cref="FixedSinceBaseline"/>). Deliberately does not track
/// renamed/removed tests or newly added passing tests — see docs/decisions/baseline-comparison.md.
/// </summary>
public sealed record BaselineComparison(
    DateTimeOffset BaselineGeneratedAt,
    IReadOnlyList<TestOutcomeEntry> NewlyFailed,
    IReadOnlyList<TestOutcomeEntry> AlreadyFailingInBaseline,
    IReadOnlyList<TestOutcomeEntry> FixedSinceBaseline);
