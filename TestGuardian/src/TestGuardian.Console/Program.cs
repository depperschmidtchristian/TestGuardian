using TestGuardian.Core;
using TestGuardian.Core.Allowlist;
using TestGuardian.Core.Allowlist.Models;
using TestGuardian.Core.Baseline;
using TestGuardian.Core.Input;
using TestGuardian.Core.Json;
using TestGuardian.Core.Json.Models;
using TestGuardian.Core.Trx;
using TestGuardian.Core.Trx.Models;
using TestGuardian.Console;
using TestGuardian.Console.Models;

if (args.Any(a => a is "--help" or "-h"))
{
    // Checked before CliArgumentParser.Parse ever runs, so --help works regardless of whatever
    // else is (or isn't) on the command line — never routed through the usage-error path.
    ConsoleReportPrinter.PrintHelp();
    return 0;
}

CliOptions options;
string? resolvedJsonPath = null;
JsonReport? baseline = null;
KnownFailureAllowlist? knownFailures = null;
try
{
    options = CliArgumentParser.Parse(args);

    if (options.JsonOutputRequested)
    {
        // An explicit path is used as-is; otherwise a default name is picked here, once — reused
        // for both this early check and the actual write at the end, so the two can never disagree.
        resolvedJsonPath = options.JsonOutputPath
            ?? JsonOutputPathValidator.GenerateDefaultPath(Directory.GetCurrentDirectory());

        // Checked as early as possible, before any .trx file is even resolved — see
        // JsonOutputPathValidator's own doc comment for why it never opens the target itself.
        JsonOutputPathValidator.EnsureCanCreate(resolvedJsonPath);
    }

    if (options.BaselinePath is { } baselinePath)
    {
        // A missing/malformed baseline file is the same class of usage error as any other bad
        // CLI input — routed through the same ArgumentException path below.
        baseline = BaselineReportLoader.Load(baselinePath) switch
        {
            BaselineLoadResult.Success success => success.Report,
            BaselineLoadResult.Failure failure => throw new ArgumentException(failure.Reason),
            var result => throw new ArgumentOutOfRangeException(nameof(result), result, "Unbekanntes BaselineLoadResult.")
        };
    }

    if (options.KnownFailuresPath is { } knownFailuresPath)
    {
        // Same class of usage error as a missing/malformed baseline — routed the same way.
        knownFailures = KnownFailureAllowlistLoader.Load(knownFailuresPath) switch
        {
            KnownFailureAllowlistLoadResult.Success success => success.Allowlist,
            KnownFailureAllowlistLoadResult.Failure failure => throw new ArgumentException(failure.Reason),
            var result => throw new ArgumentOutOfRangeException(nameof(result), result, "Unbekanntes KnownFailureAllowlistLoadResult.")
        };
    }
}
catch (ArgumentException ex)
{
    // A malformed command line is an invocation error, not a red test verdict — reported through
    // the same yellow banner style as an input-warning verdict (see docs/decisions/input-warning-level.md).
    ConsoleReportPrinter.PrintUsageError(ex.Message);
    return 1;
}

var inputResolution = TrxInputResolver.Resolve(options.Inputs, options.MaxDepth);


Console.WriteLine();

var progress = new SynchronousProgress<ReadProgress>(p =>
{
    Console.Write($"\rDatei {p.FilesRead}/{p.TotalFiles} gelesen ({p.TestsReadSoFar} Tests bisher)...");
});

var readResults = TrxBatchReader.ReadAll(inputResolution.ResolvedFilePaths, progress);
Console.WriteLine();

var overview = TestRunAggregator.Aggregate(readResults);
var verdict = TestRunVerdict.Evaluate(overview, inputResolution, options.MinTests, knownFailures);

// Purely informational — never changes the verdict/exit code itself, see docs/decisions/baseline-comparison.md.
var baselineComparison = baseline is { } baselineReport ? BaselineComparer.Compare(overview, baselineReport) : null;

ConsoleReportPrinter.Print(overview, inputResolution, verdict, baselineComparison);

if (resolvedJsonPath is { } jsonPath)
{
    try
    {
        JsonReportWriter.Write(jsonPath, new JsonReport(overview, inputResolution, verdict, DateTimeOffset.Now, baselineComparison));
        Console.WriteLine($"JSON-Bericht geschrieben nach: {jsonPath}");
    }
    catch (IOException ex)
    {
        // The early EnsureCanCreate check already ruled out the common cases; this only fires on
        // a rare TOCTOU race (e.g. the file appeared in the meantime) — FileMode.CreateNew refuses
        // to overwrite rather than risk data loss, see docs/decisions/json-output.md.
        Console.Error.WriteLine($"JSON-Bericht konnte nicht geschrieben werden: {ex.Message}");
        return 1;
    }
}

return verdict.IsSuccessful ? 0 : 1;
