using TestGuardian.Core.Input;

namespace TestGuardian.Core.Tests;

[TestClass]
public class TrxInputResolverTests
{
    private string _tempRoot = null!;

    [TestInitialize]
    public void CreateTempRoot()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"testguardian-input-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [TestCleanup]
    public void DeleteTempRoot()
    {
        Directory.Delete(_tempRoot, recursive: true);
    }

    [TestMethod]
    public void Resolve_SingleExistingFile_ReturnsThatPath()
    {
        var filePath = CreateFile("lauf.trx");

        var result = TrxInputResolver.Resolve([filePath]);

        Assert.AreEqual(1, result.ResolvedFilePaths.Count);
        Assert.AreEqual(Path.GetFullPath(filePath), result.ResolvedFilePaths[0]);
        Assert.AreEqual(0, result.UnresolvedInputs.Count);
    }

    [TestMethod]
    public void Resolve_DirectoryWithDefaultMaxDepth_ReturnsOnlyTopLevelTrxFiles()
    {
        CreateFile("a.trx");
        CreateFile(Path.Combine("sub", "b.trx"));

        var result = TrxInputResolver.Resolve([_tempRoot]);

        Assert.AreEqual(1, result.ResolvedFilePaths.Count);
        StringAssert.EndsWith(result.ResolvedFilePaths[0], "a.trx");
    }

    [TestMethod]
    public void Resolve_DirectoryWithMaxDepth_RespectsDepthLimit()
    {
        CreateFile("a.trx");
        CreateFile(Path.Combine("sub1", "b.trx"));
        CreateFile(Path.Combine("sub1", "sub2", "c.trx"));

        var result = TrxInputResolver.Resolve([_tempRoot], maxDepth: 1);

        var fileNames = result.ResolvedFilePaths.Select(Path.GetFileName).ToHashSet();
        CollectionAssert.AreEquivalent(new[] { "a.trx", "b.trx" }, fileNames.ToArray());
    }

    [TestMethod]
    public void Resolve_WildcardPattern_ReturnsMatchingFilesOnly()
    {
        CreateFile("lauf-a.trx");
        CreateFile("lauf-b.trx");
        CreateFile("andere.txt");

        var result = TrxInputResolver.Resolve([Path.Combine(_tempRoot, "lauf-*.trx")]);

        Assert.AreEqual(2, result.ResolvedFilePaths.Count);
        Assert.IsTrue(result.ResolvedFilePaths.All(p => p.EndsWith(".trx", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Resolve_NonexistentPath_AddsUnresolvedInput()
    {
        var missingPath = Path.Combine(_tempRoot, "does-not-exist", "missing.trx");

        var result = TrxInputResolver.Resolve([missingPath]);

        Assert.AreEqual(0, result.ResolvedFilePaths.Count);
        Assert.AreEqual(1, result.UnresolvedInputs.Count);
        Assert.AreEqual(missingPath, result.UnresolvedInputs[0].RawInput);
    }

    [TestMethod]
    public void Resolve_DirectoryWithNoTrxFiles_AddsUnresolvedInput()
    {
        CreateFile("readme.txt");

        var result = TrxInputResolver.Resolve([_tempRoot]);

        Assert.AreEqual(0, result.ResolvedFilePaths.Count);
        Assert.AreEqual(1, result.UnresolvedInputs.Count);
    }

    [TestMethod]
    public void Resolve_DuplicateInputs_DeduplicatesResolvedPaths()
    {
        var filePath = CreateFile("lauf.trx");

        // _tempRoot finds lauf.trx via its directory scan; filePath names the very same file
        // directly. Same physical file, reached through two different raw inputs.
        var result = TrxInputResolver.Resolve([_tempRoot, filePath]);

        Assert.AreEqual(1, result.ResolvedFilePaths.Count);
    }

    [TestMethod]
    public void Resolve_MultipleInputs_CombinesAndSortsResults()
    {
        var zFile = CreateFile("z-lauf.trx");
        var aFile = CreateFile("a-lauf.trx");

        var result = TrxInputResolver.Resolve([zFile, aFile]);

        CollectionAssert.AreEqual(
            new[] { Path.GetFullPath(aFile), Path.GetFullPath(zFile) },
            result.ResolvedFilePaths.ToArray());
    }

    private string CreateFile(string relativePath)
    {
        var fullPath = Path.Combine(_tempRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "<TestRun />");
        return fullPath;
    }
}
