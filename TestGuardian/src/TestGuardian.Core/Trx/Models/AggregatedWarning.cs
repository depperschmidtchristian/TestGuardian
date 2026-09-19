namespace TestGuardian.Core.Trx.Models;

/// <summary>
/// A <see cref="RunWarning"/> tagged with the name of the run it came from. Once multiple
/// runs are combined, an unattributed warning message would no longer say which file raised it.
/// </summary>
public sealed record AggregatedWarning(string RunName, string Message);
