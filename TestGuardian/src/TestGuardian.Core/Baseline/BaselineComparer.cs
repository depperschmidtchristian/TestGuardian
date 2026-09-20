using TestGuardian.Core.Baseline.Models;
using TestGuardian.Core.Json.Models;
using TestGuardian.Core.Trx.Models;

namespace TestGuardian.Core.Baseline;

public static class BaselineComparer
{
    public static BaselineComparison Compare(TestRunOverview current, JsonReport baseline)
    {
        var baselineOutcomes = baseline.Overview.Assemblies
            .SelectMany(a => a.TestCases)
            .ToDictionary(t => (t.AssemblyName, t.TestName), t => t.Outcome);

        var currentlyRed = current.Assemblies
            .SelectMany(a => a.TestCases)
            .Where(t => t.Outcome is TestOutcome.Failed or TestOutcome.LoadError)
            .ToList();

        var newlyFailed = new List<TestOutcomeEntry>();
        var alreadyFailing = new List<TestOutcomeEntry>();

        foreach (var test in currentlyRed)
        {
            var wasRedBefore = baselineOutcomes.TryGetValue((test.AssemblyName, test.TestName), out var oldOutcome)
                && oldOutcome is TestOutcome.Failed or TestOutcome.LoadError;

            var entry = new TestOutcomeEntry(test.AssemblyName, test.TestName, test.Outcome);
            (wasRedBefore ? alreadyFailing : newlyFailed).Add(entry);
        }

        var fixedTests = baselineOutcomes
            .Where(kv => kv.Value is TestOutcome.Failed or TestOutcome.LoadError)
            .Where(kv => currentlyRed.TrueForAll(t => (t.AssemblyName, t.TestName) != kv.Key))
            .Select(kv => new TestOutcomeEntry(kv.Key.AssemblyName, kv.Key.TestName, kv.Value))
            .ToList();

        return new BaselineComparison(baseline.GeneratedAt, newlyFailed, alreadyFailing, fixedTests);
    }
}
