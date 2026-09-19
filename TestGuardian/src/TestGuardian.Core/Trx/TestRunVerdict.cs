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

/// <summary>
/// The actual green/yellow/red decision, kept free of any console/CLI dependency so it carries the
/// same test rigor as the rest of TestGuardian.Core (see docs/decisions/exit-code-decision.md and
/// docs/decisions/input-warning-level.md).
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

        // Nur ein Rot-Grund, wenn überhaupt etwas gelesen wurde: 0 Tests bei mindestens einer
        // aufgelösten Datei ist der eigentliche "lügende grüne Balken" (z.B. lauf-b.trx — Filter
        // trifft nichts, der Lauf selbst meldet trotzdem 0 Tests). Wurde dagegen gar keine Datei
        // aufgelöst, ist die 0 nur eine Folge von UnresolvedInputs (Gelb) und würde denselben
        // Fehler doppelt melden — siehe docs/decisions/input-warning-level.md.
        if (overview.Total.CorrectlyExecuted == 0 && inputResolution.ResolvedFilePaths.Count > 0)
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

        var severity = reasons.Count == 0
            ? VerdictSeverity.Green
            : reasons.Max(r => SeverityOf(r.Kind));

        return new GuardianVerdict(severity, reasons);
    }

    /// <summary>
    /// Only <see cref="VerdictReasonKind.UnresolvedInputs"/> counts as a mere invocation/input
    /// problem (Gelb) — everything else, including <see cref="VerdictReasonKind.UnreadableFiles"/>,
    /// stays Rot because a file that was found but whose content is broken can just as well be a
    /// genuinely broken test run (see "Offene Entscheidung 1" in docs/plans/input-warning-level.md).
    /// A Rot-Grund always outranks a Gelb-Grund (enum order Green &lt; Yellow &lt; Red, combined via Max).
    /// </summary>
    private static VerdictSeverity SeverityOf(VerdictReasonKind kind) => kind switch
    {
        VerdictReasonKind.UnresolvedInputs => VerdictSeverity.Yellow,
        _ => VerdictSeverity.Red
    };
}
