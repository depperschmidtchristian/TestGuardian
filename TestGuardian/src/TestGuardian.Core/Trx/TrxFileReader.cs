namespace TestGuardian.Core.Trx;

/// <summary>
/// File-handling layer on top of <see cref="TrxDocumentParser"/>: covers the two
/// file-level cases the assignment calls out explicitly (missing file, empty file) and
/// attaches the file path to any failure the parser itself reports (e.g. malformed XML).
/// </summary>
public static class TrxFileReader
{
    public static TrxReadResult Read(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new TrxReadResult.Failure(new TrxReadFailure(
                filePath, TrxReadFailureReason.FileNotFound, $"No file found at '{filePath}'."));
        }

        var content = File.ReadAllText(filePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new TrxReadResult.Failure(new TrxReadFailure(
                filePath, TrxReadFailureReason.EmptyFile, $"File '{filePath}' is empty."));
        }

        var result = TrxDocumentParser.Parse(content);

        return result is TrxReadResult.Failure failure
            ? new TrxReadResult.Failure(failure.Error with { FilePath = filePath })
            : result;
    }
}
