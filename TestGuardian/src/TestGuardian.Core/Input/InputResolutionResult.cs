namespace TestGuardian.Core.Input;

public sealed record InputResolutionResult(
    IReadOnlyList<string> ResolvedFilePaths,
    IReadOnlyList<UnresolvedInput> UnresolvedInputs);
