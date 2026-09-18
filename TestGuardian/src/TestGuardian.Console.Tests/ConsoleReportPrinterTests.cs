using TestGuardian.Core.Input;
using TestGuardian.Core.Trx;
using TestGuardian.Console;

namespace TestGuardian.Console.Tests;

// No exact format pinning (too brittle) — content-based spot checks against redirected Console.Out
// instead, per docs/plans/exit-code-decision.md. "Console" is ambiguous in this namespace (it is
// itself named Console), so System.Console is used fully qualified throughout, same as in
// ConsoleReportPrinter itself.
[TestClass]
public class ConsoleReportPrinterTests
{
    [TestMethod]
    public void Print_SuccessfulRun_OutputContainsAssemblyNameAndGreenVerdict()
    {
        var overview = new TestRunOverview(
            [new AssemblySummary("widgetlib.testcpp.dll", [new TestCaseResult("widgetlib.testcpp.dll", "T1", TestOutcome.Passed, "Passed", null)])],
            [],
            []);
        var verdict = new GuardianVerdict(true, []);

        var output = CaptureOutput(() => ConsoleReportPrinter.Print(overview, new InputResolutionResult([], []), verdict));

        StringAssert.Contains(output, "widgetlib.testcpp.dll");
        StringAssert.Contains(output, "GRUEN");
    }

    [TestMethod]
    public void Print_FailedRun_OutputContainsAllReasonMessages()
    {
        var overview = new TestRunOverview([], [], []);
        var reasons = new List<VerdictReason>
        {
            new(VerdictReasonKind.ZeroTestsExecuted, "Es wurden 0 Tests mit eindeutigem Ergebnis ausgeführt."),
            new(VerdictReasonKind.UnresolvedInputs, "1 Eingabe(n) konnten nicht aufgelöst werden.")
        };
        var verdict = new GuardianVerdict(false, reasons);

        var output = CaptureOutput(() => ConsoleReportPrinter.Print(overview, new InputResolutionResult([], []), verdict));

        foreach (var reason in reasons)
        {
            StringAssert.Contains(output, reason.Message);
        }
        StringAssert.Contains(output, "ROT");
    }

    private static string CaptureOutput(Action action)
    {
        var originalOut = System.Console.Out;
        var writer = new StringWriter();
        System.Console.SetOut(writer);
        try
        {
            action();
        }
        finally
        {
            System.Console.SetOut(originalOut);
        }

        return writer.ToString();
    }
}
