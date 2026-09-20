namespace TestGuardian.Console.Models;

/// <summary>
/// <see cref="JsonOutputRequested"/> and <see cref="JsonOutputPath"/> are deliberately separate:
/// <c>--to-json</c> can be given without a value to request the default
/// <c>testguardian_report_&lt;n&gt;.json</c> naming (resolved later, once the current directory's
/// existing files can be checked) — <see cref="JsonOutputPath"/> alone can't distinguish
/// "not requested" from "requested, use the default name" if it stayed the sole signal.
/// </summary>
public sealed record CliOptions(
    IReadOnlyList<string> Inputs,
    int MaxDepth,
    int? MinTests,
    bool JsonOutputRequested,
    string? JsonOutputPath);
