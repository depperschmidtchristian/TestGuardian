namespace TestGuardian.Core.Json.Models;

/// <summary>
/// A missing or malformed baseline file is an expected case, not an exceptional one — same
/// reasoning as <see cref="TestGuardian.Core.Trx.Models.TrxReadResult"/> for .trx files.
/// </summary>
public abstract record BaselineLoadResult
{
    private BaselineLoadResult() { }

    public sealed record Success(JsonReport Report) : BaselineLoadResult;

    public sealed record Failure(string Reason) : BaselineLoadResult;
}
