namespace TestGuardian.Core.Allowlist.Models;

/// <summary>
/// A missing allowlist file is an expected case, not an exceptional one — same reasoning as
/// <see cref="TestGuardian.Core.Trx.Models.TrxReadResult"/> and
/// <see cref="TestGuardian.Core.Json.Models.BaselineLoadResult"/>.
/// </summary>
public abstract record KnownFailureAllowlistLoadResult
{
    private KnownFailureAllowlistLoadResult() { }

    public sealed record Success(KnownFailureAllowlist Allowlist) : KnownFailureAllowlistLoadResult;

    public sealed record Failure(string Reason) : KnownFailureAllowlistLoadResult;
}
