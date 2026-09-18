namespace TestGuardian.Core.Trx;

/// <summary>
/// The combined view over every run handed to <see cref="TestRunAggregator.Aggregate"/>:
/// per-assembly summaries, files that could not be read at all, and warnings from every
/// successfully parsed run. <see cref="Total"/> is computed from <see cref="Assemblies"/>
/// rather than tracked separately, for the same reason <see cref="AssemblySummary"/> computes
/// its own counts: one source of truth.
/// </summary>
public sealed record TestRunOverview(
    IReadOnlyList<AssemblySummary> Assemblies,
    IReadOnlyList<TrxReadFailure> FileFailures,
    IReadOnlyList<AggregatedWarning> Warnings)
{
    public AssemblySummary Total => new("Gesamt", Assemblies.SelectMany(a => a.TestCases).ToList());
}
