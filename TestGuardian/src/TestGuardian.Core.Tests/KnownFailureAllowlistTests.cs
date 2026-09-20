using TestGuardian.Core.Allowlist;

namespace TestGuardian.Core.Tests;

[TestClass]
public class KnownFailureAllowlistTests
{
    [TestMethod]
    public void Parse_PlainTestNames_AllAreContained()
    {
        var allowlist = KnownFailureAllowlist.Parse(["Test_A", "Test_B"]);

        Assert.IsTrue(allowlist.Contains("Test_A"));
        Assert.IsTrue(allowlist.Contains("Test_B"));
    }

    [TestMethod]
    public void Parse_CommentAndBlankLines_AreIgnored()
    {
        var allowlist = KnownFailureAllowlist.Parse(
        [
            "# Kommentarzeile",
            "",
            "   ",
            "Test_A"
        ]);

        Assert.IsTrue(allowlist.Contains("Test_A"));
        Assert.IsFalse(allowlist.Contains("# Kommentarzeile"));
    }

    [TestMethod]
    public void Parse_LeadingAndTrailingWhitespace_IsTrimmed()
    {
        var allowlist = KnownFailureAllowlist.Parse(["   Test_A   "]);

        Assert.IsTrue(allowlist.Contains("Test_A"));
    }

    [TestMethod]
    public void Contains_TestNameNotOnList_ReturnsFalse()
    {
        var allowlist = KnownFailureAllowlist.Parse(["Test_A"]);

        Assert.IsFalse(allowlist.Contains("Test_B"));
    }

    [TestMethod]
    public void Empty_ContainsNothing()
    {
        Assert.IsFalse(KnownFailureAllowlist.Empty.Contains("Test_A"));
    }
}
