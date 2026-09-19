using TestGuardian.Core.Trx;
using TestGuardian.Core.Trx.Models;

namespace TestGuardian.Core.Tests;

// SynchronousProgress<T> lives in TestGuardian.Core (shared with Program.cs) — see its doc comment.

[TestClass]
public class TrxBatchReaderTests
{
    [TestMethod]
    public void ReadAll_MultipleFiles_ReturnsResultsInSameOrderAsInput()
    {
        var paths = new[] { SampleData.PathTo("lauf-a.trx"), SampleData.PathTo("lauf-d.trx") };

        var results = TrxBatchReader.ReadAll(paths);

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("Beispiellauf Gemischt", ((TrxReadResult.Success)results[0]).Run.RunName);
        Assert.AreEqual("Beispiellauf Gruen", ((TrxReadResult.Success)results[1]).Run.RunName);
    }

    [TestMethod]
    public void ReadAll_ReportsProgressAfterEachFile_WithRunningTestCount()
    {
        var paths = new[] { SampleData.PathTo("lauf-a.trx"), SampleData.PathTo("lauf-d.trx") }; // 7 + 3 test cases
        var reports = new List<ReadProgress>();

        TrxBatchReader.ReadAll(paths, new SynchronousProgress<ReadProgress>(reports.Add));

        Assert.AreEqual(2, reports.Count);
        Assert.AreEqual(1, reports[0].FilesRead);
        Assert.AreEqual(2, reports[0].TotalFiles);
        Assert.AreEqual(7, reports[0].TestsReadSoFar);
        Assert.AreEqual(2, reports[1].FilesRead);
        Assert.AreEqual(10, reports[1].TestsReadSoFar);
    }

    [TestMethod]
    public void ReadAll_UnreadableFileAmongOthers_ContinuesBatchAndReportsZeroTestsForIt()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.trx");
        var paths = new[] { SampleData.PathTo("lauf-a.trx"), missingPath, SampleData.PathTo("lauf-d.trx") };
        var reports = new List<ReadProgress>();

        var results = TrxBatchReader.ReadAll(paths, new SynchronousProgress<ReadProgress>(reports.Add));

        Assert.IsInstanceOfType<TrxReadResult.Failure>(results[1]);
        Assert.AreEqual(7, reports[1].TestsReadSoFar, "A missing file must not add to the running test count.");
        Assert.AreEqual(10, reports[2].TestsReadSoFar, "The batch must continue past the unreadable file.");
    }

    [TestMethod]
    public void ReadAll_EmptyFileList_ReturnsEmptyResultAndNoProgressReports()
    {
        var reports = new List<ReadProgress>();

        var results = TrxBatchReader.ReadAll([], new SynchronousProgress<ReadProgress>(reports.Add));

        Assert.AreEqual(0, results.Count);
        Assert.AreEqual(0, reports.Count);
    }
}
