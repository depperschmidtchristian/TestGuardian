namespace TestGuardian.Core.Trx;

/// <summary>
/// Reported once per file by <see cref="TrxBatchReader.ReadAll"/>. <see cref="TestsReadSoFar"/>
/// is a running total across all files processed so far, not a fraction of a known grand
/// total — see docs/plans/multi-file-input.md for why no upfront total is estimated.
/// </summary>
public sealed record ReadProgress(int FilesRead, int TotalFiles, int TestsReadSoFar);
