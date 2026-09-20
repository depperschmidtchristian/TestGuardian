using TestGuardian.Console;

namespace TestGuardian.Console.Tests;

[TestClass]
public class CliArgumentParserTests
{
    [TestMethod]
    public void Parse_OnlyInputs_DefaultsMaxDepthToZeroAndMinTestsToNull()
    {
        var options = CliArgumentParser.Parse(["a.trx", "b.trx"]);

        CollectionAssert.AreEqual(new[] { "a.trx", "b.trx" }, options.Inputs.ToArray());
        Assert.AreEqual(0, options.MaxDepth);
        Assert.IsNull(options.MinTests);
    }

    [TestMethod]
    public void Parse_MaxDepthWithoutValue_UsesDefaultTen()
    {
        var options = CliArgumentParser.Parse(["a.trx", "--max-depth"]);

        Assert.AreEqual(10, options.MaxDepth);
    }

    [TestMethod]
    public void Parse_MaxDepthWithValue_UsesGivenValue()
    {
        var options = CliArgumentParser.Parse(["a.trx", "--max-depth", "3"]);

        Assert.AreEqual(3, options.MaxDepth);
    }

    [TestMethod]
    public void Parse_MinTestsWithValue_SetsMinTests()
    {
        var options = CliArgumentParser.Parse(["a.trx", "--min-tests", "5"]);

        Assert.AreEqual(5, options.MinTests);
    }

    [TestMethod]
    public void Parse_MinTestsWithoutValue_Throws()
    {
        Assert.ThrowsException<ArgumentException>(() => CliArgumentParser.Parse(["a.trx", "--min-tests"]));
    }

    [TestMethod]
    public void Parse_UnknownOption_ThrowsInsteadOfBecomingAPositionalInput()
    {
        // A typo'd switch (e.g. "--min-tets") must not silently turn into a bogus file input that
        // then fails as an "unresolved input" — that hides a usage error behind a misleading verdict.
        Assert.ThrowsException<ArgumentException>(() => CliArgumentParser.Parse(["a.trx", "--min-tets", "3"]));
    }

    [TestMethod]
    public void Parse_NoInputsAtAll_Throws()
    {
        // Otherwise neither ResolvedFilePaths nor UnresolvedInputs ends up with anything to report,
        // and the run silently comes back GRUEN despite 0 tests ever having executed.
        Assert.ThrowsException<ArgumentException>(() => CliArgumentParser.Parse([]));
    }

    [TestMethod]
    public void Parse_OnlyOptionsNoPositionalInputs_Throws()
    {
        Assert.ThrowsException<ArgumentException>(() => CliArgumentParser.Parse(["--max-depth", "3"]));
    }

    [TestMethod]
    public void Parse_MixedInputsAndOptions_SeparatesCorrectly()
    {
        var options = CliArgumentParser.Parse(["a.trx", "--max-depth", "2", "b.trx"]);

        CollectionAssert.AreEqual(new[] { "a.trx", "b.trx" }, options.Inputs.ToArray());
        Assert.AreEqual(2, options.MaxDepth);
    }

    [TestMethod]
    public void Parse_ToJsonWithValue_SetsRequestedAndJsonOutputPath()
    {
        var options = CliArgumentParser.Parse(["a.trx", "--to-json", "report.json"]);

        Assert.IsTrue(options.JsonOutputRequested);
        Assert.AreEqual("report.json", options.JsonOutputPath);
    }

    [TestMethod]
    public void Parse_ToJsonWithoutValue_SetsRequestedTrueAndPathNull()
    {
        // No explicit path — Program.cs is expected to generate a default testguardian_report_<n>.json
        // name later, once the current directory's existing files can actually be checked.
        var options = CliArgumentParser.Parse(["a.trx", "--to-json"]);

        Assert.IsTrue(options.JsonOutputRequested);
        Assert.IsNull(options.JsonOutputPath);
    }

    [TestMethod]
    public void Parse_ToJsonImmediatelyFollowedByAnotherOption_DoesNotSwallowItAsThePath()
    {
        var options = CliArgumentParser.Parse(["a.trx", "--to-json", "--max-depth", "3"]);

        Assert.IsTrue(options.JsonOutputRequested);
        Assert.IsNull(options.JsonOutputPath);
        Assert.AreEqual(3, options.MaxDepth, "--max-depth must still be parsed as its own option, not as --to-json's value.");
    }

    [TestMethod]
    public void Parse_WithoutToJson_JsonOutputNotRequestedAndPathIsNull()
    {
        var options = CliArgumentParser.Parse(["a.trx"]);

        Assert.IsFalse(options.JsonOutputRequested);
        Assert.IsNull(options.JsonOutputPath);
    }
}
