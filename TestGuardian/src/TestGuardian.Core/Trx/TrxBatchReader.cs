using TestGuardian.Core.Trx.Models;

namespace TestGuardian.Core.Trx;

/// <summary>
/// Reads a list of resolved .trx paths one by one via <see cref="TrxFileReader"/>, reporting
/// progress after each file so a caller (the CLI) can tell a slow run from a stuck one.
/// </summary>
public static class TrxBatchReader
{
    public static IReadOnlyList<TrxReadResult> ReadAll(
        IReadOnlyList<string> filePaths,
        IProgress<ReadProgress>? progress = null)
    {
        var results = new List<TrxReadResult>(filePaths.Count);
        var testsReadSoFar = 0;

        for (var i = 0; i < filePaths.Count; i++)
        {
            var result = TrxFileReader.Read(filePaths[i]);
            results.Add(result);

            if (result is TrxReadResult.Success success)
            {
                testsReadSoFar += success.Run.TestCases.Count;
            }

            progress?.Report(new ReadProgress(i + 1, filePaths.Count, testsReadSoFar));
        }

        return results;
    }
}
