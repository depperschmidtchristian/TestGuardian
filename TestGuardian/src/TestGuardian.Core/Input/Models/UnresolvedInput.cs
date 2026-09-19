namespace TestGuardian.Core.Input.Models;

/// <summary>
/// A raw input string (path or pattern) that did not resolve to any file — a likely
/// typo or misconfigured path, distinct from "file found but contains zero tests".
/// </summary>
public sealed record UnresolvedInput(string RawInput, string Reason);
