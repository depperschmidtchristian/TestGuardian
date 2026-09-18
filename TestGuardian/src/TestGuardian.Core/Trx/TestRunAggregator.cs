namespace TestGuardian.Core.Trx;

/// <summary>
/// Combines the results of reading one or more .trx files into a single overview,
/// grouped by assembly rather than by source file (see docs/plans/trx-aggregation-summary.md).
/// </summary>
public static class TestRunAggregator
{
    public static TestRunOverview Aggregate(IEnumerable<TrxReadResult> results)
    {
        var successfulRuns = new List<TestRunResult>();
        var fileFailures = new List<TrxReadFailure>();

        foreach (var result in results)
        {
            switch (result)
            {
                case TrxReadResult.Success success:
                    successfulRuns.Add(success.Run);
                    break;
                case TrxReadResult.Failure failure:
                    fileFailures.Add(failure.Error);
                    break;
            }
        }

        var assemblies = successfulRuns
            .SelectMany(run => run.TestCases)
            .GroupBy(testCase => testCase.AssemblyName)
            .Select(group => new AssemblySummary(group.Key, group.ToList()))
            .OrderBy(summary => summary.AssemblyName, StringComparer.Ordinal)
            .ToList();

        var warnings = successfulRuns
            .SelectMany(run => run.Warnings.Select(warning => new AggregatedWarning(run.RunName, warning.Message)))
            .ToList();

        return new TestRunOverview(assemblies, fileFailures, warnings);
    }
}
