using TestGuardian.Core.Allowlist;
using TestGuardian.Core.Input.Models;
using TestGuardian.Core.Trx;
using TestGuardian.Core.Trx.Models;

namespace TestGuardian.Core.Tests;

[TestClass]
public class TestRunVerdictTests
{
    private static readonly InputResolutionResult NoUnresolvedInputs = new([], []);

    [TestMethod]
    public void Evaluate_AllPassedNoMinTests_IsSuccessful()
    {
        var overview = AggregateSamples("lauf-d.trx");

        var verdict = TestRunVerdict.Evaluate(overview, NoUnresolvedInputs, minTests: null);

        Assert.IsTrue(verdict.IsSuccessful);
        Assert.AreEqual(VerdictSeverity.Green, verdict.Severity);
        Assert.AreEqual(0, verdict.Reasons.Count);
    }

    [TestMethod]
    public void Evaluate_RealFailuresPresent_IsUnsuccessfulWithRealTestFailuresReason()
    {
        var overview = AggregateSamples("lauf-a.trx");

        var verdict = TestRunVerdict.Evaluate(overview, NoUnresolvedInputs, minTests: null);

        Assert.IsFalse(verdict.IsSuccessful);
        Assert.IsTrue(verdict.Reasons.Any(r => r.Kind == VerdictReasonKind.RealTestFailures));
    }

    [TestMethod]
    public void Evaluate_LoadErrorAlonePresent_IsUnsuccessfulEvenWithoutRealFailures()
    {
        var overview = AggregateSamples("lauf-c.trx"); // 1 passed, 3 load errors, 0 real failures

        var verdict = TestRunVerdict.Evaluate(overview, NoUnresolvedInputs, minTests: null);

        Assert.IsFalse(verdict.IsSuccessful);
        Assert.IsTrue(verdict.Reasons.Any(r => r.Kind == VerdictReasonKind.LoadErrors));
        Assert.IsFalse(verdict.Reasons.Any(r => r.Kind == VerdictReasonKind.RealTestFailures),
            "Load errors must never masquerade as real test failures.");
    }

    [TestMethod]
    public void Evaluate_ZeroCorrectlyExecuted_IsUnsuccessfulWithZeroTestsReason()
    {
        var overview = AggregateSamples("lauf-b.trx"); // 0 tests, filter matched nothing
        // lauf-b.trx WAS actually resolved and read — unlike NoUnresolvedInputs, which the other
        // tests use as a "no CLI input issues" placeholder without reflecting what got aggregated.
        var inputResolution = new InputResolutionResult([SampleData.PathTo("lauf-b.trx")], []);

        var verdict = TestRunVerdict.Evaluate(overview, inputResolution, minTests: null);

        Assert.IsFalse(verdict.IsSuccessful);
        Assert.AreEqual(VerdictSeverity.Red, verdict.Severity,
            "A run that was actually read and reports 0 tests is the genuine 'lying green bar' case — stays Red.");
        Assert.IsTrue(verdict.Reasons.Any(r => r.Kind == VerdictReasonKind.ZeroTestsExecuted));
    }

    [TestMethod]
    public void Evaluate_NothingResolvedAtAll_ZeroTestsIsNotAddedOnTopOfUnresolvedInputs()
    {
        // The scenario that surfaced this: "TestGuardian *.trx --max-depth 5" in a folder with no
        // .trx directly in it — the pattern resolves nothing, so overview is empty (0 tests) purely
        // as a consequence of the input problem. That must not ALSO raise a Red ZeroTestsExecuted
        // reason on top of the already-Yellow UnresolvedInputs one for the same root cause.
        var overview = new TestRunOverview([], [], []);
        var inputResolution = new InputResolutionResult(
            [],
            [new UnresolvedInput("*.trx", "Das Suchmuster '*.trx' hat keine Datei getroffen.")]);

        var verdict = TestRunVerdict.Evaluate(overview, inputResolution, minTests: null);

        Assert.AreEqual(VerdictSeverity.Yellow, verdict.Severity);
        Assert.IsFalse(verdict.Reasons.Any(r => r.Kind == VerdictReasonKind.ZeroTestsExecuted));
        Assert.IsTrue(verdict.Reasons.Any(r => r.Kind == VerdictReasonKind.UnresolvedInputs));
    }

    [TestMethod]
    public void Evaluate_BelowMinTests_IsUnsuccessful()
    {
        var overview = AggregateSamples("lauf-d.trx"); // 3 tests

        var verdict = TestRunVerdict.Evaluate(overview, NoUnresolvedInputs, minTests: 5);

        Assert.IsFalse(verdict.IsSuccessful);
        Assert.IsTrue(verdict.Reasons.Any(r => r.Kind == VerdictReasonKind.BelowMinimumTestCount));
    }

    [TestMethod]
    public void Evaluate_AtOrAboveMinTests_IsSuccessful()
    {
        var overview = AggregateSamples("lauf-d.trx"); // 3 tests

        var verdict = TestRunVerdict.Evaluate(overview, NoUnresolvedInputs, minTests: 3);

        Assert.IsTrue(verdict.IsSuccessful);
    }

    [TestMethod]
    public void Evaluate_FileFailurePresent_IsUnsuccessful()
    {
        var runD = ParseSample("lauf-d.trx");
        var brokenFile = new TrxReadResult.Failure(
            new TrxReadFailure("missing.trx", TrxReadFailureReason.FileNotFound, "No file found at 'missing.trx'."));
        var overview = TestRunAggregator.Aggregate([runD, brokenFile]);

        var verdict = TestRunVerdict.Evaluate(overview, NoUnresolvedInputs, minTests: null);

        Assert.IsFalse(verdict.IsSuccessful);
        Assert.AreEqual(VerdictSeverity.Red, verdict.Severity,
            "A file that was found but whose content is broken stays Rot, unlike an unresolved input — see docs/plans/input-warning-level.md.");
        Assert.IsTrue(verdict.Reasons.Any(r => r.Kind == VerdictReasonKind.UnreadableFiles));
    }

    [TestMethod]
    public void Evaluate_UnresolvedInputPresent_IsYellowNotRed()
    {
        var overview = AggregateSamples("lauf-d.trx"); // otherwise a clean green run
        var inputResolution = new InputResolutionResult(
            [],
            [new UnresolvedInput("does-not-exist.trx", "Datei nicht gefunden.")]);

        var verdict = TestRunVerdict.Evaluate(overview, inputResolution, minTests: null);

        Assert.IsFalse(verdict.IsSuccessful);
        Assert.AreEqual(VerdictSeverity.Yellow, verdict.Severity);
        Assert.IsTrue(verdict.Reasons.Any(r => r.Kind == VerdictReasonKind.UnresolvedInputs));
    }

    [TestMethod]
    public void Evaluate_UnresolvedInputAndRealFailureCoexist_SeverityIsRed()
    {
        var overview = AggregateSamples("lauf-a.trx"); // real failures present
        var inputResolution = new InputResolutionResult(
            [],
            [new UnresolvedInput("does-not-exist.trx", "Datei nicht gefunden.")]);

        var verdict = TestRunVerdict.Evaluate(overview, inputResolution, minTests: null);

        Assert.AreEqual(VerdictSeverity.Red, verdict.Severity,
            "A real red reason must never be masked by a yellow input problem (Vorrangregel).");
        Assert.IsTrue(verdict.Reasons.Any(r => r.Kind == VerdictReasonKind.RealTestFailures));
        Assert.IsTrue(verdict.Reasons.Any(r => r.Kind == VerdictReasonKind.UnresolvedInputs),
            "The yellow reason is still reported alongside the red one, it just doesn't decide the banner color.");
    }

    [TestMethod]
    public void Evaluate_AllFailuresOnAllowlist_IsSuccessfulAndReportsThemAsTolerated()
    {
        var overview = AggregateSamples("lauf-a.trx"); // 4 passed, 3 failed
        var allowlist = KnownFailureAllowlist.Parse(
            ["Pfad_UncPfad_WirdErkannt", "Puffer_Nullzeiger_WirdAbgefangen", "Wandler_LeereEingabe_LiefertLeer"]);

        var verdict = TestRunVerdict.Evaluate(overview, NoUnresolvedInputs, minTests: null, allowlist);

        Assert.IsTrue(verdict.IsSuccessful,
            "All three failures are known and tolerated — nothing unexpected is left to make this Red.");
        Assert.AreEqual(0, verdict.Reasons.Count);
        Assert.AreEqual(3, verdict.ToleratedFailures.Count,
            "Tolerated failures must stay visible even though they no longer affect the verdict.");
    }

    [TestMethod]
    public void Evaluate_SomeFailuresOnAllowlistOthersNot_StaysRedForTheUnexpectedOnes()
    {
        var overview = AggregateSamples("lauf-a.trx"); // 4 passed, 3 failed
        var allowlist = KnownFailureAllowlist.Parse(["Pfad_UncPfad_WirdErkannt"]); // only 1 of 3

        var verdict = TestRunVerdict.Evaluate(overview, NoUnresolvedInputs, minTests: null, allowlist);

        Assert.AreEqual(VerdictSeverity.Red, verdict.Severity);
        Assert.AreEqual(1, verdict.ToleratedFailures.Count);
        var realFailuresReason = verdict.Reasons.Single(r => r.Kind == VerdictReasonKind.RealTestFailures);
        StringAssert.Contains(realFailuresReason.Message, "2 Test(s)",
            "Only the 2 non-allowlisted failures should count towards the Red reason.");
    }

    [TestMethod]
    public void Evaluate_WithoutAllowlist_ToleratedFailuresIsEmpty()
    {
        var overview = AggregateSamples("lauf-a.trx");

        var verdict = TestRunVerdict.Evaluate(overview, NoUnresolvedInputs, minTests: null);

        Assert.AreEqual(0, verdict.ToleratedFailures.Count);
    }

    [TestMethod]
    public void Evaluate_MultipleReasonsCanCoexist()
    {
        var overview = AggregateSamples("lauf-a.trx", "lauf-c.trx"); // real failures + load errors

        var verdict = TestRunVerdict.Evaluate(overview, NoUnresolvedInputs, minTests: null);

        Assert.IsFalse(verdict.IsSuccessful);
        Assert.IsTrue(verdict.Reasons.Any(r => r.Kind == VerdictReasonKind.RealTestFailures));
        Assert.IsTrue(verdict.Reasons.Any(r => r.Kind == VerdictReasonKind.LoadErrors));
    }

    private static TestRunOverview AggregateSamples(params string[] fileNames) =>
        TestRunAggregator.Aggregate(fileNames.Select(ParseSample));

    private static TrxReadResult ParseSample(string fileName) =>
        TrxDocumentParser.Parse(File.ReadAllText(SampleData.PathTo(fileName)));
}
