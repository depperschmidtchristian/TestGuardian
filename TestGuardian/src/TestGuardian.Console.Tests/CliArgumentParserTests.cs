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
}
