using TestGuardian.Core.Input.Models;
using TestGuardian.Core.Json;
using TestGuardian.Core.Json.Models;
using TestGuardian.Core.Trx.Models;

namespace TestGuardian.Core.Tests;

[TestClass]
public class BaselineReportLoaderTests
{
    private string _tempRoot = null!;

    [TestInitialize]
    public void CreateTempRoot()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"testguardian-baseline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [TestCleanup]
    public void DeleteTempRoot()
    {
        Directory.Delete(_tempRoot, recursive: true);
    }

    [TestMethod]
    public void Load_ValidReportWrittenByJsonReportWriter_RoundTripsTheOutcome()
    {
        var path = Path.Combine(_tempRoot, "baseline.json");
        var generatedAt = new DateTimeOffset(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);
        var overview = new TestRunOverview(
            [new AssemblySummary("lib.dll", [new TestCaseResult("lib.dll", "T1", TestOutcome.Failed, "Failed", "boom")])],
            [],
            []);
        var report = new JsonReport(overview, new InputResolutionResult([], []), new GuardianVerdict(VerdictSeverity.Red, []), generatedAt);
        JsonReportWriter.Write(path, report);

        var result = BaselineReportLoader.Load(path);

        var success = result as BaselineLoadResult.Success;
        Assert.IsNotNull(success);
        Assert.AreEqual(generatedAt, success!.Report.GeneratedAt);
        Assert.AreEqual(TestOutcome.Failed, success.Report.Overview.Assemblies[0].TestCases[0].Outcome);
    }

    [TestMethod]
    public void Load_MissingFile_ReturnsFailure()
    {
        var path = Path.Combine(_tempRoot, "does-not-exist.json");

        var result = BaselineReportLoader.Load(path);

        Assert.IsInstanceOfType<BaselineLoadResult.Failure>(result);
    }

    [TestMethod]
    public void Load_MalformedJson_ReturnsFailureInsteadOfThrowing()
    {
        var path = Path.Combine(_tempRoot, "malformed.json");
        File.WriteAllText(path, "{ this is not valid json");

        var result = BaselineReportLoader.Load(path);

        Assert.IsInstanceOfType<BaselineLoadResult.Failure>(result);
    }
}
