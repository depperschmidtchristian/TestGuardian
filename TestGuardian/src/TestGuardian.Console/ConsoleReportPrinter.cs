using TestGuardian.Core.Input;
using TestGuardian.Core.Trx;

namespace TestGuardian.Console;

/// <summary>
/// Pure formatting, no decision logic — the actual verdict is already decided by the time this
/// runs (see <see cref="TestRunVerdict"/> in TestGuardian.Core). Bare "Console" is ambiguous here:
/// this namespace is itself named "Console", so every reference below is fully qualified as
/// System.Console to avoid resolving to this namespace instead of the BCL type (confirmed via CS0234).
/// </summary>
public static class ConsoleReportPrinter
{
    public static void Print(
        TestRunOverview overview,
        InputResolutionResult inputResolution,
        GuardianVerdict verdict)
    {
        System.Console.WriteLine();
        System.Console.WriteLine("Ergebnis je Bibliothek:");
        foreach (var assembly in overview.Assemblies)
        {
            System.Console.WriteLine(FormatAssemblyLine(assembly));
        }

        System.Console.WriteLine(new string('-', 70));
        System.Console.WriteLine(FormatAssemblyLine(overview.Total));

        PrintSectionIfAny("Warnungen", overview.Warnings,
            w => $"[{w.RunName}] {w.Message}");
        PrintSectionIfAny("Nicht lesbare Dateien", overview.FileFailures,
            f => $"[{f.FilePath}] {f.Detail}");
        PrintSectionIfAny("Nicht aufgelöste Eingaben", inputResolution.UnresolvedInputs,
            u => $"[{u.RawInput}] {u.Reason}");

        System.Console.WriteLine();
        PrintVerdict(verdict);
    }

    private static string FormatAssemblyLine(AssemblySummary summary) =>
        $"  {summary.AssemblyName,-40} {summary.Total,4} gesamt  {summary.Passed,4} bestanden  " +
        $"{summary.Failed,4} fehlgeschlagen  {summary.LoadError,4} Ladefehler  {summary.Skipped,4} übersprungen";

    private static void PrintSectionIfAny<T>(string heading, IReadOnlyList<T> items, Func<T, string> formatLine)
    {
        if (items.Count == 0)
        {
            return;
        }

        System.Console.WriteLine();
        System.Console.WriteLine($"{heading}:");
        foreach (var item in items)
        {
            System.Console.WriteLine($"  {formatLine(item)}");
        }
    }

    private static void PrintVerdict(GuardianVerdict verdict)
    {
        var (color, label) = verdict.IsSuccessful
            ? (ConsoleColor.Green, "GRUEN")
            : (ConsoleColor.Red, "ROT");
        var bar = new string('#', 10);

        WriteLineInColor($"{bar} URTEIL: {label} {bar}", color);

        foreach (var reason in verdict.Reasons)
        {
            System.Console.WriteLine($"  - {reason.Message}");
        }
    }

    /// <summary>
    /// Setting <see cref="System.Console.ForegroundColor"/> throws <see cref="IOException"/> when
    /// there is no real console attached (e.g. a redirected/captured stream in a test host) —
    /// caught here so the report still prints in plain text instead of crashing in that situation.
    /// </summary>
    private static void WriteLineInColor(string text, ConsoleColor color)
    {
        ConsoleColor? original = null;
        try
        {
            original = System.Console.ForegroundColor;
            System.Console.ForegroundColor = color;
        }
        catch (IOException)
        {
        }

        System.Console.WriteLine(text);

        if (original is { } previousColor)
        {
            try
            {
                System.Console.ForegroundColor = previousColor;
            }
            catch (IOException)
            {
            }
        }
    }
}
