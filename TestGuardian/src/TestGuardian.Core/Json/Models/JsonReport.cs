using TestGuardian.Core.Input.Models;
using TestGuardian.Core.Trx.Models;

namespace TestGuardian.Core.Json.Models;

/// <summary>
/// The full report — the same pieces the console printer already formats as text — bundled
/// here for machine-readable output instead. See docs/decisions/json-output.md for why this
/// carries the whole report, not just the raw <see cref="TestRunOverview"/>.
/// </summary>
public sealed record JsonReport(
    TestRunOverview Overview,
    InputResolutionResult InputResolution,
    GuardianVerdict Verdict);
