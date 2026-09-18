using TestGuardian.Core.Trx;

namespace TestGuardian.Core.Tests;

[TestClass]
public class TrxDocumentParserTests
{
    [TestMethod]
    public void Parse_MixedRun_ClassifiesPassedAndFailed()
    {
        var xml = File.ReadAllText(SampleData.PathTo("lauf-a.trx"));

        var result = TrxDocumentParser.Parse(xml);

        var success = result as TrxReadResult.Success;
        Assert.IsNotNull(success, "Expected a well-formed, matching run to parse successfully.");
        Assert.AreEqual(7, success!.Run.TestCases.Count);
        Assert.AreEqual(4, success.Run.TestCases.Count(t => t.Outcome == TestOutcome.Passed));
        Assert.AreEqual(3, success.Run.TestCases.Count(t => t.Outcome == TestOutcome.Failed));
        Assert.AreEqual(0, success.Run.TestCases.Count(t => t.Outcome == TestOutcome.LoadError));
        Assert.AreEqual(0, success.Run.Warnings.Count, "Declared counters match the actual results; no warning expected.");
    }

    [TestMethod]
    public void Parse_EmptyFilterRun_ZeroTestsWithFilterWarning()
    {
        var xml = File.ReadAllText(SampleData.PathTo("lauf-b.trx"));

        var result = TrxDocumentParser.Parse(xml);

        var success = result as TrxReadResult.Success;
        Assert.IsNotNull(success, "A run with zero matching tests is still well-formed XML and must parse.");
        Assert.AreEqual(0, success!.Run.TestCases.Count, "The whole point of this fixture: zero tests actually ran.");
        Assert.AreEqual(1, success.Run.Warnings.Count);
        StringAssert.Contains(success.Run.Warnings[0].Message, "Kein Test entspricht dem angegebenen Testfallfilter");
    }

    [TestMethod]
    public void Parse_LoadErrorRun_SeparatesLoadErrorFromFailed()
    {
        var xml = File.ReadAllText(SampleData.PathTo("lauf-c.trx"));

        var result = TrxDocumentParser.Parse(xml);

        var success = result as TrxReadResult.Success;
        Assert.IsNotNull(success);
        Assert.AreEqual(1, success!.Run.TestCases.Count(t => t.Outcome == TestOutcome.Passed));
        Assert.AreEqual(0, success.Run.TestCases.Count(t => t.Outcome == TestOutcome.Failed),
            "Load errors must not be counted as real test failures.");
        Assert.AreEqual(3, success.Run.TestCases.Count(t => t.Outcome == TestOutcome.LoadError));
    }

    [TestMethod]
    public void Parse_CleanGreenRun_AllPassedNoWarnings()
    {
        var xml = File.ReadAllText(SampleData.PathTo("lauf-d.trx"));

        var result = TrxDocumentParser.Parse(xml);

        var success = result as TrxReadResult.Success;
        Assert.IsNotNull(success);
        Assert.AreEqual(3, success!.Run.TestCases.Count);
        Assert.IsTrue(success.Run.TestCases.All(t => t.Outcome == TestOutcome.Passed));
        Assert.AreEqual(0, success.Run.Warnings.Count, "A genuinely clean run must not raise false alarms.");
    }

    [TestMethod]
    public void Parse_TruncatedXml_ReturnsMalformedXmlFailure()
    {
        var xml = File.ReadAllText(SampleData.PathTo("lauf-e.trx"));

        var result = TrxDocumentParser.Parse(xml);

        var failure = result as TrxReadResult.Failure;
        Assert.IsNotNull(failure, "A cut-off, unclosed-tag file must not be silently treated as a valid run.");
        Assert.AreEqual(TrxReadFailureReason.MalformedXml, failure!.Error.Reason);
    }

    [TestMethod]
    public void Parse_CountersMismatch_AddsWarning()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <TestRun id="11111111-1111-1111-1111-111111111111" name="Synthetic Mismatch" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <ResultSummary outcome="Completed">
                <Counters total="5" executed="5" passed="5" failed="0" />
              </ResultSummary>
              <TestDefinitions>
                <UnitTest name="OnlyTest" storage="synthetic.dll" id="id-1">
                  <Execution id="exec-1" />
                  <TestMethod codeBase="synthetic.dll" className="C" name="OnlyTest" />
                </UnitTest>
              </TestDefinitions>
              <Results>
                <UnitTestResult executionId="exec-1" testId="id-1" testName="OnlyTest" outcome="Passed" />
              </Results>
            </TestRun>
            """;

        var result = TrxDocumentParser.Parse(xml);

        var success = result as TrxReadResult.Success;
        Assert.IsNotNull(success, "The XML itself is well-formed; only the declared counters are wrong.");
        Assert.AreEqual(1, success!.Run.TestCases.Count, "Only one <UnitTestResult> actually exists.");
        Assert.AreEqual(1, success.Run.Warnings.Count);
        StringAssert.Contains(success.Run.Warnings[0].Message, "declared 5, actually found 1");
    }

    [TestMethod]
    public void Parse_UnknownOutcome_MapsToOtherWithRawOutcomePreserved()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <TestRun id="22222222-2222-2222-2222-222222222222" name="Synthetic Unknown Outcome" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <ResultSummary outcome="Completed">
                <Counters total="1" executed="1" passed="0" failed="0" />
              </ResultSummary>
              <TestDefinitions>
                <UnitTest name="FlakyTest" storage="synthetic.dll" id="id-1">
                  <Execution id="exec-1" />
                  <TestMethod codeBase="synthetic.dll" className="C" name="FlakyTest" />
                </UnitTest>
              </TestDefinitions>
              <Results>
                <UnitTestResult executionId="exec-1" testId="id-1" testName="FlakyTest" outcome="Inconclusive" />
              </Results>
            </TestRun>
            """;

        var result = TrxDocumentParser.Parse(xml);

        var success = result as TrxReadResult.Success;
        Assert.IsNotNull(success);
        var testCase = success!.Run.TestCases.Single();
        Assert.AreEqual(TestOutcome.Other, testCase.Outcome, "Unrecognized outcomes must not be guessed as Passed or Failed.");
        Assert.AreEqual("Inconclusive", testCase.RawOutcome, "The original outcome text must not be lost.");
        Assert.IsTrue(success.Run.Warnings.Any(w => w.Message.Contains("Inconclusive")));
    }
}
