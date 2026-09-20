using TestGuardian.Console;

namespace TestGuardian.Console.Tests;

[TestClass]
public class JsonOutputPathValidatorTests
{
    private string _tempRoot = null!;

    [TestInitialize]
    public void CreateTempRoot()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"testguardian-json-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [TestCleanup]
    public void DeleteTempRoot()
    {
        Directory.Delete(_tempRoot, recursive: true);
    }

    [TestMethod]
    public void EnsureCanCreate_PathInExistingDirectoryFileNotYetPresent_DoesNotThrow()
    {
        var path = Path.Combine(_tempRoot, "report.json");

        JsonOutputPathValidator.EnsureCanCreate(path);
    }

    [TestMethod]
    public void EnsureCanCreate_FileAlreadyExists_ThrowsWithoutTouchingIt()
    {
        var path = Path.Combine(_tempRoot, "report.json");
        const string originalContent = "already here — must survive untouched";
        File.WriteAllText(path, originalContent);

        Assert.ThrowsException<ArgumentException>(() => JsonOutputPathValidator.EnsureCanCreate(path));
        Assert.AreEqual(originalContent, File.ReadAllText(path),
            "The whole point of this check: an existing file must never be opened, let alone modified.");
    }

    [TestMethod]
    public void EnsureCanCreate_TargetDirectoryDoesNotExist_Throws()
    {
        var path = Path.Combine(_tempRoot, "does-not-exist", "report.json");

        Assert.ThrowsException<ArgumentException>(() => JsonOutputPathValidator.EnsureCanCreate(path));
    }

    [TestMethod]
    public void GenerateDefaultPath_NoExistingReportInDirectory_UsesNumberOne()
    {
        var path = JsonOutputPathValidator.GenerateDefaultPath(_tempRoot);

        Assert.AreEqual(Path.Combine(_tempRoot, "testguardian_report_1.json"), path);
    }

    [TestMethod]
    public void GenerateDefaultPath_SomeNumbersAlreadyTaken_SkipsToFirstFreeOne()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "testguardian_report_1.json"), "taken");
        File.WriteAllText(Path.Combine(_tempRoot, "testguardian_report_2.json"), "taken");

        var path = JsonOutputPathValidator.GenerateDefaultPath(_tempRoot);

        Assert.AreEqual(Path.Combine(_tempRoot, "testguardian_report_3.json"), path);
    }

    [TestMethod]
    public void GenerateDefaultPath_ReturnedPathDoesNotExistYet()
    {
        var path = JsonOutputPathValidator.GenerateDefaultPath(_tempRoot);

        Assert.IsFalse(File.Exists(path), "Must only pick a free name, never create the file itself.");
    }
}
