using TestGuardian.Core.Input;

namespace TestGuardian.Core.Trx;

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

public sealed record GuardianVerdict(bool IsSuccessful, IReadOnlyList<VerdictReason> Reasons);

/// <summary>
/// The actual green/red decision, kept free of any console/CLI dependency so it carries the
/// same test rigor as the rest of TestGuardian.Core (see docs/decisions/exit-code-decision.md).
/// </summary>
public static class TestRunVerdict
{
    public static GuardianVerdict Evaluate(
        TestRunOverview overview,
        InputResolutionResult inputResolution,
        int? minTests)
    {
        var reasons = new List<VerdictReason>();

        if (overview.Total.Failed > 0)
        {
            reasons.Add(new VerdictReason(VerdictReasonKind.RealTestFailures,
                $"{overview.Total.Failed} Test(s) fehlgeschlagen."));
        }

        if (overview.Total.LoadError > 0)
        {
            reasons.Add(new VerdictReason(VerdictReasonKind.LoadErrors,
                $"{overview.Total.LoadError} Test(s) konnten nicht geladen werden (Bibliothek nicht ladbar)."));
        }

        if (overview.Total.CorrectlyExecuted == 0)
        {
            reasons.Add(new VerdictReason(VerdictReasonKind.ZeroTestsExecuted,
                "Es wurden 0 Tests mit eindeutigem Ergebnis ausgeführt."));
        }

        if (minTests is int min && overview.Total.CorrectlyExecuted < min)
        {
            reasons.Add(new VerdictReason(VerdictReasonKind.BelowMinimumTestCount,
                $"Nur {overview.Total.CorrectlyExecuted} von mindestens {min} erwarteten Tests liefen."));
        }

        if (overview.FileFailures.Count > 0)
        {
            reasons.Add(new VerdictReason(VerdictReasonKind.UnreadableFiles,
                $"{overview.FileFailures.Count} Datei(en) konnten nicht gelesen werden."));
        }

        if (inputResolution.UnresolvedInputs.Count > 0)
        {
            reasons.Add(new VerdictReason(VerdictReasonKind.UnresolvedInputs,
                $"{inputResolution.UnresolvedInputs.Count} Eingabe(n) konnten nicht aufgelöst werden."));
        }

        return new GuardianVerdict(reasons.Count == 0, reasons);
    }
}
