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

public sealed record GuardianVerdict(VerdictSeverity Severity, IReadOnlyList<VerdictReason> Reasons)
{
    // Exit-Code-Entscheidung bleibt binär: alles außer Green scheitert (siehe docs/decisions/input-warning-level.md).
    public bool IsSuccessful => Severity == VerdictSeverity.Green;
}
