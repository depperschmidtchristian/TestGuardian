using TestGuardian.Core.Trx;

namespace TestGuardian.Core.Tests;

[TestClass]
public class TestRunAggregatorTests
{
    [TestMethod]
    public void Aggregate_MultipleRunsOfSameAssembly_MergesCountsAcrossFiles()
    {
        var runA = ParseSample("lauf-a.trx"); // widgetlib.testcpp.dll: 4 passed, 3 failed
        var runD = ParseSample("lauf-d.trx"); // widgetlib.testcpp.dll: 3 passed, 0 failed

        var overview = TestRunAggregator.Aggregate([runA, runD]);

        Assert.AreEqual(1, overview.Assemblies.Count, "Both files report the same assembly; expected one merged row.");
        var widgetlib = overview.Assemblies.Single();
        Assert.AreEqual("widgetlib.testcpp.dll", widgetlib.AssemblyName);
        Assert.AreEqual(7, widgetlib.Passed);
        Assert.AreEqual(3, widgetlib.Failed);
        Assert.AreEqual(10, widgetlib.Total);
    }

    [TestMethod]
    public void Aggregate_LoadErrorAndPassed_ProducesSeparateAssemblySummaries()
    {
        var runC = ParseSample("lauf-c.trx"); // widgetlib.testcpp.dll: 1 passed; dienstlib.testcpp.dll: 3 load errors

        var overview = TestRunAggregator.Aggregate([runC]);

        Assert.AreEqual(2, overview.Assemblies.Count);
        var dienstlib = overview.Assemblies.Single(a => a.AssemblyName == "dienstlib.testcpp.dll");
        Assert.AreEqual(3, dienstlib.LoadError);
        Assert.AreEqual(0, dienstlib.Failed, "Load errors must never show up as real failures, even after aggregation.");
        var widgetlib = overview.Assemblies.Single(a => a.AssemblyName == "widgetlib.testcpp.dll");
        Assert.AreEqual(1, widgetlib.Passed);
    }

    [TestMethod]
    public void Aggregate_UnreadableFile_AddsToFileFailuresWithoutCrashing()
    {
        var runA = ParseSample("lauf-a.trx");
        var brokenFile = new TrxReadResult.Failure(
            new TrxReadFailure("missing.trx", TrxReadFailureReason.FileNotFound, "No file found at 'missing.trx'."));

        var overview = TestRunAggregator.Aggregate([runA, brokenFile]);

        Assert.AreEqual(1, overview.FileFailures.Count);
        Assert.AreEqual("missing.trx", overview.FileFailures[0].FilePath);
        Assert.AreEqual(1, overview.Assemblies.Count, "The unreadable file must not swallow the results from the good one.");
    }

    [TestMethod]
    public void Aggregate_NoResults_ProducesEmptyOverviewWithZeroTotal()
    {
        var overview = TestRunAggregator.Aggregate([]);

        Assert.AreEqual(0, overview.Assemblies.Count);
        Assert.AreEqual(0, overview.Total.Total);
    }

    [TestMethod]
    public void Aggregate_Warnings_AreTaggedWithOriginatingRunName()
    {
        var runB = ParseSample("lauf-b.trx"); // "Beispiellauf Leer", one RunInfo warning

        var overview = TestRunAggregator.Aggregate([runB]);

        Assert.AreEqual(1, overview.Warnings.Count);
        Assert.AreEqual("Beispiellauf Leer", overview.Warnings[0].RunName);
        StringAssert.Contains(overview.Warnings[0].Message, "Kein Test entspricht dem angegebenen Testfallfilter");
    }

    [TestMethod]
    public void Aggregate_Assemblies_AreSortedAlphabetically()
    {
        var zLib = new TrxReadResult.Success(new TestRunResult(
            "Run Z",
            [new TestCaseResult("z-lib.dll", "T", TestOutcome.Passed, "Passed", null)],
            []));
        var aLib = new TrxReadResult.Success(new TestRunResult(
            "Run A",
            [new TestCaseResult("a-lib.dll", "T", TestOutcome.Passed, "Passed", null)],
            []));

        var overview = TestRunAggregator.Aggregate([zLib, aLib]);

        CollectionAssert.AreEqual(
            new[] { "a-lib.dll", "z-lib.dll" },
            overview.Assemblies.Select(a => a.AssemblyName).ToArray());
    }

    [TestMethod]
    public void AssemblySummary_Executed_ExcludesLoadErrorAndSkipped()
    {
        var testCases = new List<TestCaseResult>
        {
            new("lib.dll", "Passed1", TestOutcome.Passed, "Passed", null),
            new("lib.dll", "Failed1", TestOutcome.Failed, "Failed", "boom"),
            new("lib.dll", "LoadError1", TestOutcome.LoadError, "Failed", "Failed to load the test assembly or its dependencies"),
            new("lib.dll", "Skipped1", TestOutcome.Skipped, "NotExecuted", null),
            new("lib.dll", "Other1", TestOutcome.Other, "Inconclusive", null)
        };

        var summary = new AssemblySummary("lib.dll", testCases);

        Assert.AreEqual(5, summary.Total);
        Assert.AreEqual(2, summary.Executed, "Only Passed, Failed, and Other count as 'executed'.");
    }

    [TestMethod]
    public void TestRunOverview_Total_SumsAcrossAllAssemblySummaries()
    {
        var runA = ParseSample("lauf-a.trx"); // 7 test cases
        var runC = ParseSample("lauf-c.trx"); // 4 test cases

        var overview = TestRunAggregator.Aggregate([runA, runC]);

        Assert.AreEqual(11, overview.Total.Total);
    }

    private static TrxReadResult ParseSample(string fileName) =>
        TrxDocumentParser.Parse(File.ReadAllText(SampleData.PathTo(fileName)));
}
