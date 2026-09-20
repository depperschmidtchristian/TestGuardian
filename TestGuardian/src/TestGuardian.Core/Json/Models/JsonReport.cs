using TestGuardian.Core.Baseline.Models;
using TestGuardian.Core.Input.Models;
using TestGuardian.Core.Trx.Models;

namespace TestGuardian.Core.Json.Models;

/// <summary>
/// The full report — the same pieces the console printer already formats as text — bundled
/// here for machine-readable output instead. See docs/decisions/json-output.md for why this
/// carries the whole report, not just the raw <see cref="TestRunOverview"/>. <see cref="GeneratedAt"/>
/// is set by the caller, not by <see cref="JsonReportWriter"/> itself, so a report's content never
/// depends on ambient clock state — see docs/decisions/baseline-comparison.md. <see cref="BaselineComparison"/>
/// is null unless <c>--baseline</c> was also given.
/// </summary>
public sealed record JsonReport(
    TestRunOverview Overview,
    InputResolutionResult InputResolution,
    GuardianVerdict Verdict,
    DateTimeOffset GeneratedAt,
    BaselineComparison? BaselineComparison = null);
