using TestGuardian.Core.Baseline;
using TestGuardian.Core.Input.Models;
using TestGuardian.Core.Json.Models;
using TestGuardian.Core.Trx.Models;

namespace TestGuardian.Core.Tests;

[TestClass]
public class BaselineComparerTests
{
    private static readonly DateTimeOffset BaselineTimestamp = new(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Compare_TestFailingNowAndInBaseline_IsAlreadyFailingInBaseline()
    {
        var current = Overview(("lib.dll", "T1", TestOutcome.Failed));
        var baseline = Baseline(("lib.dll", "T1", TestOutcome.Failed));

        var result = BaselineComparer.Compare(current, baseline);

        Assert.AreEqual(1, result.AlreadyFailingInBaseline.Count);
        Assert.AreEqual(0, result.NewlyFailed.Count);
        Assert.AreEqual(0, result.FixedSinceBaseline.Count);
    }

    [TestMethod]
    public void Compare_TestFailingNowButPassedInBaseline_IsNewlyFailed()
    {
        var current = Overview(("lib.dll", "T1", TestOutcome.Failed));
        var baseline = Baseline(("lib.dll", "T1", TestOutcome.Passed));

        var result = BaselineComparer.Compare(current, baseline);

        Assert.AreEqual(1, result.NewlyFailed.Count);
        Assert.AreEqual(0, result.AlreadyFailingInBaseline.Count);
    }

    [TestMethod]
    public void Compare_TestFailingNowWithNoBaselineCounterpart_IsNewlyFailed()
    {
        var current = Overview(("lib.dll", "BrandNewTest", TestOutcome.Failed));
        var baseline = Baseline(("lib.dll", "SomeOtherTest", TestOutcome.Passed));

        var result = BaselineComparer.Compare(current, baseline);

        Assert.AreEqual(1, result.NewlyFailed.Count);
        Assert.AreEqual("BrandNewTest", result.NewlyFailed[0].TestName);
    }

    [TestMethod]
    public void Compare_TestFailingInBaselineButPassingNow_IsFixedSinceBaseline()
    {
        var current = Overview(("lib.dll", "T1", TestOutcome.Passed));
        var baseline = Baseline(("lib.dll", "T1", TestOutcome.Failed));

        var result = BaselineComparer.Compare(current, baseline);

        Assert.AreEqual(1, result.FixedSinceBaseline.Count);
        Assert.AreEqual(0, result.NewlyFailed.Count);
        Assert.AreEqual(0, result.AlreadyFailingInBaseline.Count);
    }

    [TestMethod]
    public void Compare_LoadErrorCountsAsFailingJustLikeRealFailure()
    {
        var current = Overview(("lib.dll", "T1", TestOutcome.LoadError));
        var baseline = Baseline(("lib.dll", "T1", TestOutcome.LoadError));

        var result = BaselineComparer.Compare(current, baseline);

        Assert.AreEqual(1, result.AlreadyFailingInBaseline.Count);
    }

    [TestMethod]
    public void Compare_ResultCarriesBaselineTimestamp()
    {
        var current = Overview(("lib.dll", "T1", TestOutcome.Passed));
        var baseline = Baseline(("lib.dll", "T1", TestOutcome.Passed));

        var result = BaselineComparer.Compare(current, baseline);

        Assert.AreEqual(BaselineTimestamp, result.BaselineGeneratedAt);
    }

    private static TestRunOverview Overview(params (string Assembly, string Test, TestOutcome Outcome)[] cases) =>
        new(
            [new AssemblySummary("lib.dll", cases.Select(c => new TestCaseResult(c.Assembly, c.Test, c.Outcome, c.Outcome.ToString(), null)).ToList())],
            [],
            []);

    private static JsonReport Baseline(params (string Assembly, string Test, TestOutcome Outcome)[] cases) =>
        new(Overview(cases), new InputResolutionResult([], []), new GuardianVerdict(VerdictSeverity.Green, []), BaselineTimestamp);
}
