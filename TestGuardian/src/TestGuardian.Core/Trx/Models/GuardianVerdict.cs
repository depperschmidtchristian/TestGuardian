namespace TestGuardian.Core.Trx.Models;

public enum VerdictReasonKind
{
    RealTestFailures,
    LoadErrors,
    ZeroTestsExecuted,
    BelowMinimumTestCount,
    UnreadableFiles,
    UnresolvedInputs
}

/// <summary>
/// <see cref="Kind"/> is the stable value tests and callers should branch on; <see cref="Message"/>
/// is only the human-readable text for the console report and may change wording independently.
/// </summary>
public sealed record VerdictReason(VerdictReasonKind Kind, string Message);

public enum VerdictSeverity
{
    Green,
    Yellow,
    Red
}

/// <summary>
/// <see cref="ToleratedFailures"/> defaults to empty so existing two-argument call sites keep
/// compiling — tests/callers that don't know about the known-failures allowlist (see
/// docs/decisions/known-failures-allowlist.md) simply never populate it.
/// </summary>
public sealed record GuardianVerdict(
    VerdictSeverity Severity,
    IReadOnlyList<VerdictReason> Reasons,
    IReadOnlyList<TestOutcomeEntry>? ToleratedFailures = null)
{
    // Redeclared (not just the positional parameter) so a null argument becomes an empty list —
    // callers never need to check for null, only Count == 0.
    public IReadOnlyList<TestOutcomeEntry> ToleratedFailures { get; init; } = ToleratedFailures ?? [];

    // Exit-Code-Entscheidung bleibt binär: alles außer Green scheitert (siehe docs/decisions/input-warning-level.md).
    public bool IsSuccessful => Severity == VerdictSeverity.Green;
}
