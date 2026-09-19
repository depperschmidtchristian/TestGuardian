namespace TestGuardian.Core.Trx.Models;

/// <summary>
/// A notable condition found while reading a run that does not by itself make the
/// file unreadable, but that a caller should not silently ignore (e.g. a test-filter
/// match warning, or declared counters that disagree with the actual results).
/// </summary>
public sealed record RunWarning(string Message);
