using System.Text.Json;
using TestGuardian.Core.Input.Models;
using TestGuardian.Core.Json;
using TestGuardian.Core.Json.Models;
using TestGuardian.Core.Trx.Models;

namespace TestGuardian.Core.Tests;

[TestClass]
public class JsonReportWriterTests
{
    private string _tempRoot = null!;

    [TestInitialize]
    public void CreateTempRoot()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"testguardian-jsonwriter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [TestCleanup]
    public void DeleteTempRoot()
    {
        Directory.Delete(_tempRoot, recursive: true);
    }

    [TestMethod]
    public void Write_TypicalReport_ProducesParsableJsonWithExpectedFields()
    {
        var path = Path.Combine(_tempRoot, "report.json");
        var overview = new TestRunOverview(
            [new AssemblySummary("widgetlib.testcpp.dll", [new TestCaseResult("widgetlib.testcpp.dll", "T1", TestOutcome.Passed, "Passed", null)])],
            [],
            []);
        var inputResolution = new InputResolutionResult(
            ["widgetlib.testcpp.trx"],
            [new UnresolvedInput("missing.trx", "Datei nicht gefunden.")]);
        var verdict = new GuardianVerdict(VerdictSeverity.Yellow, [new VerdictReason(VerdictReasonKind.UnresolvedInputs, "1 Eingabe(n) konnten nicht aufgelöst werden.")]);

        var generatedAt = new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.Zero);

        JsonReportWriter.Write(path, new JsonReport(overview, inputResolution, verdict, generatedAt));

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        Assert.AreEqual("widgetlib.testcpp.dll",
            root.GetProperty("overview").GetProperty("assemblies")[0].GetProperty("assemblyName").GetString());
        Assert.AreEqual("Yellow", root.GetProperty("verdict").GetProperty("severity").GetString(),
            "Enums must serialize as their name, not as a number — a numeric value would be fragile for downstream consumers.");
        Assert.AreEqual("missing.trx",
            root.GetProperty("inputResolution").GetProperty("unresolvedInputs")[0].GetProperty("rawInput").GetString());
        Assert.AreEqual(generatedAt, root.GetProperty("generatedAt").GetDateTimeOffset());
        Assert.IsFalse(root.TryGetProperty("baselineComparison", out var baselineElement) && baselineElement.ValueKind != JsonValueKind.Null,
            "No --baseline given — the field must be absent or null, not an empty object.");
    }

    [TestMethod]
    public void Write_TargetFileAlreadyExists_ThrowsAndLeavesOriginalContentUntouched()
    {
        var path = Path.Combine(_tempRoot, "report.json");
        const string originalContent = "already here — must survive untouched";
        File.WriteAllText(path, originalContent);
        var report = new JsonReport(new TestRunOverview([], [], []), new InputResolutionResult([], []), new GuardianVerdict(VerdictSeverity.Green, []), DateTimeOffset.Now);

        Assert.ThrowsException<IOException>(() => JsonReportWriter.Write(path, report));
        Assert.AreEqual(originalContent, File.ReadAllText(path),
            "FileMode.CreateNew must refuse to overwrite rather than risk data loss.");
    }
}
