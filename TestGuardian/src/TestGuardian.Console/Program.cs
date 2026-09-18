using TestGuardian.Core;
using TestGuardian.Core.Input;
using TestGuardian.Core.Trx;
using TestGuardian.Console;

var options = CliArgumentParser.Parse(args);

var inputResolution = TrxInputResolver.Resolve(options.Inputs, options.MaxDepth);

var progress = new SynchronousProgress<ReadProgress>(p =>
    Console.Write($"\rDatei {p.FilesRead}/{p.TotalFiles} gelesen ({p.TestsReadSoFar} Tests bisher)..."));
var readResults = TrxBatchReader.ReadAll(inputResolution.ResolvedFilePaths, progress);
Console.WriteLine();

var overview = TestRunAggregator.Aggregate(readResults);
var verdict = TestRunVerdict.Evaluate(overview, inputResolution, options.MinTests);

ConsoleReportPrinter.Print(overview, inputResolution, verdict);

return verdict.IsSuccessful ? 0 : 1;
