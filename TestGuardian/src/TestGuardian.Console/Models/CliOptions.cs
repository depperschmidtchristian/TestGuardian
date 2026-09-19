namespace TestGuardian.Console.Models;

public sealed record CliOptions(IReadOnlyList<string> Inputs, int MaxDepth, int? MinTests);
