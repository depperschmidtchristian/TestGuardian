using TestGuardian.Core.Trx;
using TestGuardian.Core.Trx.Models;

namespace TestGuardian.Core.Tests;

[TestClass]
public class TrxFileReaderTests
{
    [TestMethod]
    public void Read_MissingFile_ReturnsFileNotFoundFailure()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.trx");

        var result = TrxFileReader.Read(missingPath);

        var failure = result as TrxReadResult.Failure;
        Assert.IsNotNull(failure, "A missing file must be reported, never silently treated as success.");
        Assert.AreEqual(TrxReadFailureReason.FileNotFound, failure!.Error.Reason);
        Assert.AreEqual(missingPath, failure.Error.FilePath);
    }

    [TestMethod]
    public void Read_EmptyFile_ReturnsEmptyFileFailure()
    {
        var emptyPath = Path.Combine(Path.GetTempPath(), $"empty-{Guid.NewGuid():N}.trx");
        File.WriteAllText(emptyPath, string.Empty);

        try
        {
            var result = TrxFileReader.Read(emptyPath);

            var failure = result as TrxReadResult.Failure;
            Assert.IsNotNull(failure, "An empty file must be reported, never silently treated as success.");
            Assert.AreEqual(TrxReadFailureReason.EmptyFile, failure!.Error.Reason);
            Assert.AreEqual(emptyPath, failure.Error.FilePath);
        }
        finally
        {
            File.Delete(emptyPath);
        }
    }

    [TestMethod]
    public void Read_MalformedFile_AttachesFilePathToParserFailure()
    {
        var malformedPath = Path.Combine(Path.GetTempPath(), $"malformed-{Guid.NewGuid():N}.trx");
        File.WriteAllText(malformedPath, "<TestRun><Unclosed>");

        try
        {
            var result = TrxFileReader.Read(malformedPath);

            var failure = result as TrxReadResult.Failure;
            Assert.IsNotNull(failure);
            Assert.AreEqual(TrxReadFailureReason.MalformedXml, failure!.Error.Reason);
            Assert.AreEqual(malformedPath, failure.Error.FilePath,
                "TrxFileReader must attach the file path even though TrxDocumentParser itself does not know it.");
        }
        finally
        {
            File.Delete(malformedPath);
        }
    }
}
