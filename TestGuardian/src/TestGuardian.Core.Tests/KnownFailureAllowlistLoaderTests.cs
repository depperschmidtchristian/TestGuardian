using TestGuardian.Core.Allowlist;
using TestGuardian.Core.Allowlist.Models;

namespace TestGuardian.Core.Tests;

[TestClass]
public class KnownFailureAllowlistLoaderTests
{
    private string _tempRoot = null!;

    [TestInitialize]
    public void CreateTempRoot()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"testguardian-allowlist-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [TestCleanup]
    public void DeleteTempRoot()
    {
        Directory.Delete(_tempRoot, recursive: true);
    }

    [TestMethod]
    public void Load_ExistingFile_ReturnsSuccessWithParsedNames()
    {
        var path = Path.Combine(_tempRoot, "allowlist.txt");
        File.WriteAllLines(path, ["# Kommentar", "Test_RequiresLicensedFeature"]);

        var result = KnownFailureAllowlistLoader.Load(path);

        var success = result as KnownFailureAllowlistLoadResult.Success;
        Assert.IsNotNull(success);
        Assert.IsTrue(success!.Allowlist.Contains("Test_RequiresLicensedFeature"));
    }

    [TestMethod]
    public void Load_MissingFile_ReturnsFailure()
    {
        var path = Path.Combine(_tempRoot, "does-not-exist.txt");

        var result = KnownFailureAllowlistLoader.Load(path);

        Assert.IsInstanceOfType<KnownFailureAllowlistLoadResult.Failure>(result);
    }
}
