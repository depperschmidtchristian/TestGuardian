using System.Xml;
using System.Xml.Linq;

namespace TestGuardian.Core.Trx;

/// <summary>
/// Parses the XML content of a .trx run. Pure and file-system-free on purpose, so the
/// classification rules can be unit-tested without touching disk; see <see cref="TrxFileReader"/>
/// for the file-handling layer built on top of this.
/// </summary>
public static class TrxDocumentParser
{
    private static readonly XNamespace Ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    private const string LoadFailureMarker = "Failed to load the test assembly or its dependencies";

    public static TrxReadResult Parse(string xmlContent)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xmlContent);
        }
        catch (XmlException ex)
        {
            return new TrxReadResult.Failure(new TrxReadFailure(null, TrxReadFailureReason.MalformedXml, ex.Message));
        }

        var testRun = document.Root;
        if (testRun is null || testRun.Name != Ns + "TestRun")
        {
            return new TrxReadResult.Failure(new TrxReadFailure(
                null,
                TrxReadFailureReason.MalformedXml,
                "Root element is not a <TestRun> in the expected VisualStudio TeamTest namespace."));
        }

        var runName = (string?)testRun.Attribute("name") ?? "(unnamed run)";
        var assemblyByTestId = BuildAssemblyLookup(testRun);

        var warnings = new List<RunWarning>();
        var testCases = ReadTestCases(testRun, assemblyByTestId, warnings);

        AppendRunInfoWarnings(testRun, warnings);
        AppendCountersMismatchWarning(testRun, testCases, warnings);

        return new TrxReadResult.Success(new TestRunResult(runName, testCases, warnings));
    }

    private static Dictionary<string, string> BuildAssemblyLookup(XElement testRun) =>
        testRun.Descendants(Ns + "UnitTest")
            .Select(unitTest => new
            {
                Id = (string?)unitTest.Attribute("id"),
                Storage = (string?)unitTest.Attribute("storage")
            })
            .Where(entry => entry.Id is not null)
            .ToDictionary(entry => entry.Id!, entry => entry.Storage ?? "(unknown assembly)");

    private static List<TestCaseResult> ReadTestCases(
        XElement testRun,
        IReadOnlyDictionary<string, string> assemblyByTestId,
        List<RunWarning> warnings)
    {
        var testCases = new List<TestCaseResult>();

        foreach (var resultElement in testRun.Descendants(Ns + "UnitTestResult"))
        {
            var testId = (string?)resultElement.Attribute("testId");
            var testName = (string?)resultElement.Attribute("testName") ?? "(unnamed test)";
            var rawOutcome = (string?)resultElement.Attribute("outcome") ?? "(no outcome)";
            var errorMessage = resultElement.Descendants(Ns + "Message").FirstOrDefault()?.Value;

            string assemblyName;
            if (testId is not null && assemblyByTestId.TryGetValue(testId, out var storage))
            {
                assemblyName = storage;
            }
            else
            {
                assemblyName = "(unknown assembly)";
                warnings.Add(new RunWarning(
                    $"Test '{testName}' has no matching <UnitTest> definition; its assembly could not be determined."));
            }

            var outcome = ClassifyOutcome(rawOutcome, errorMessage, testName, warnings);
            testCases.Add(new TestCaseResult(assemblyName, testName, outcome, rawOutcome, errorMessage));
        }

        return testCases;
    }

    private static TestOutcome ClassifyOutcome(
        string rawOutcome,
        string? errorMessage,
        string testName,
        List<RunWarning> warnings)
    {
        switch (rawOutcome)
        {
            case "Passed":
                return TestOutcome.Passed;
            case "Failed" when errorMessage?.Contains(LoadFailureMarker, StringComparison.Ordinal) == true:
                return TestOutcome.LoadError;
            case "Failed":
                return TestOutcome.Failed;
            case "NotExecuted":
                return TestOutcome.Skipped;
            default:
                warnings.Add(new RunWarning(
                    $"Test '{testName}' has an unrecognized outcome '{rawOutcome}'; classified as Other rather than guessed."));
                return TestOutcome.Other;
        }
    }

    private static void AppendRunInfoWarnings(XElement testRun, List<RunWarning> warnings)
    {
        foreach (var runInfo in testRun.Descendants(Ns + "RunInfo"))
        {
            var text = runInfo.Element(Ns + "Text")?.Value;
            if (!string.IsNullOrWhiteSpace(text))
            {
                warnings.Add(new RunWarning(text.Trim()));
            }
        }
    }

    private static void AppendCountersMismatchWarning(
        XElement testRun,
        IReadOnlyList<TestCaseResult> testCases,
        List<RunWarning> warnings)
    {
        var counters = testRun.Element(Ns + "ResultSummary")?.Element(Ns + "Counters");
        if (counters is null)
        {
            return;
        }

        var declaredTotal = (int?)counters.Attribute("total");
        var declaredPassed = (int?)counters.Attribute("passed");
        var declaredFailed = (int?)counters.Attribute("failed");

        var actualTotal = testCases.Count;
        var actualPassed = testCases.Count(t => t.Outcome == TestOutcome.Passed);
        var actualFailed = testCases.Count(t => t.Outcome is TestOutcome.Failed or TestOutcome.LoadError);

        var mismatches = new List<string>();
        if (declaredTotal is not null && declaredTotal != actualTotal)
        {
            mismatches.Add($"total: declared {declaredTotal}, actually found {actualTotal}");
        }

        if (declaredPassed is not null && declaredPassed != actualPassed)
        {
            mismatches.Add($"passed: declared {declaredPassed}, actually found {actualPassed}");
        }

        if (declaredFailed is not null && declaredFailed != actualFailed)
        {
            mismatches.Add($"failed: declared {declaredFailed}, actually found {actualFailed}");
        }

        if (mismatches.Count > 0)
        {
            warnings.Add(new RunWarning(
                $"Declared <Counters> do not match the actual results ({string.Join("; ", mismatches)})."));
        }
    }
}
