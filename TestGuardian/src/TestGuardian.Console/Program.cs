using TestGuardian.Core;
using TestGuardian.Core.Input;
using TestGuardian.Core.Trx;
using TestGuardian.Console;

CliOptions options;
try
{
    options = CliArgumentParser.Parse(args);
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

return verdict.IsSuccessful ? 0 : 1;
