namespace TestGuardian.Core.Trx;

/// <summary>
/// Closed result of reading a run: either it succeeded, or it failed for one of the
/// reasons in <see cref="TrxReadFailureReason"/>. Modeled as a type instead of exceptions
/// because a missing/malformed/empty file is an expected case here, not an exceptional one.
/// </summary>
public abstract record TrxReadResult
{
    private TrxReadResult() { }

    public sealed record Success(TestRunResult Run) : TrxReadResult;

    public sealed record Failure(TrxReadFailure Error) : TrxReadResult;
}
