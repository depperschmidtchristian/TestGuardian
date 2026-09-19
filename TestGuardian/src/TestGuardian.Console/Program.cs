using TestGuardian.Core;
using TestGuardian.Core.Input;
using TestGuardian.Core.Json;
using TestGuardian.Core.Json.Models;
using TestGuardian.Core.Trx;
using TestGuardian.Core.Trx.Models;
using TestGuardian.Console;
using TestGuardian.Console.Models;

CliOptions options;
try
{
    options = CliArgumentParser.Parse(args);

    if (options.JsonOutputPath is { } requestedJsonPath)
    {
        // Checked as early as possible, before any .trx file is even resolved — see
        // JsonOutputPathValidator's own doc comment for why it never opens the target itself.
        JsonOutputPathValidator.EnsureCanCreate(requestedJsonPath);
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
var verdict = TestRunVerdict.Evaluate(overview, inputResolution, options.MinTests);

ConsoleReportPrinter.Print(overview, inputResolution, verdict);

if (options.JsonOutputPath is { } jsonPath)
{
    try
    {
        JsonReportWriter.Write(jsonPath, new JsonReport(overview, inputResolution, verdict));
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
