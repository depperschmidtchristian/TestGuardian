namespace TestGuardian.Core.Trx.Models;

/// <summary>
/// A single test identified by name (and, where meaningful, its assembly) together with the
/// outcome it had at some point in time — used both for baseline comparison and for reporting
/// which failures were tolerated via the known-failures allowlist.
/// </summary>
public sealed record TestOutcomeEntry(string AssemblyName, string TestName, TestOutcome Outcome);
