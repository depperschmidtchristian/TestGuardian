using TestGuardian.Core.Allowlist;
using TestGuardian.Core.Input.Models;
using TestGuardian.Core.Trx.Models;

namespace TestGuardian.Core.Trx;

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
        int? minTests,
        KnownFailureAllowlist? knownFailures = null)
    {
        var reasons = new List<VerdictReason>();

        // Tolerated failures are excluded from the Failed/LoadError counts that feed the verdict
        // (but not from CorrectlyExecuted — the test body did run and reach an unambiguous result)
        // — they're reported separately (GuardianVerdict.ToleratedFailures) so they stay visible,
        // never silently dropped. See docs/decisions/known-failures-allowlist.md.
        var allowlist = knownFailures ?? KnownFailureAllowlist.Empty;
        var toleratedFailures = new List<TestOutcomeEntry>();
        var unexpectedFailed = 0;
        var unexpectedLoadErrors = 0;

        foreach (var testCase in overview.Total.TestCases)
        {
            if (testCase.Outcome is not (TestOutcome.Failed or TestOutcome.LoadError))
            {
                continue;
            }

            if (allowlist.Contains(testCase.TestName))
            {
                toleratedFailures.Add(new TestOutcomeEntry(testCase.AssemblyName, testCase.TestName, testCase.Outcome));
            }
            else if (testCase.Outcome == TestOutcome.Failed)
            {
                unexpectedFailed++;
            }
            else
            {
                unexpectedLoadErrors++;
            }
        }

        if (unexpectedFailed > 0)
        {
            reasons.Add(new VerdictReason(VerdictReasonKind.RealTestFailures,
                $"{unexpectedFailed} Test(s) fehlgeschlagen."));
        }

        if (unexpectedLoadErrors > 0)
        {
            reasons.Add(new VerdictReason(VerdictReasonKind.LoadErrors,
                $"{unexpectedLoadErrors} Test(s) konnten nicht geladen werden (Bibliothek nicht ladbar)."));
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

        return new GuardianVerdict(severity, reasons, toleratedFailures);
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
