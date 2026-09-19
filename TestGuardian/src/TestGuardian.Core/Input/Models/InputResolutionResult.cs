namespace TestGuardian.Core.Input.Models;

public sealed record InputResolutionResult(
    IReadOnlyList<string> ResolvedFilePaths,
    IReadOnlyList<UnresolvedInput> UnresolvedInputs);
