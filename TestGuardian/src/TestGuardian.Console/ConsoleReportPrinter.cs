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
        foreach (var assembly in overview.Assemblies)
        {
            System.Console.WriteLine(
                $"{assembly.AssemblyName}: {assembly.Total} gesamt, {assembly.Passed} bestanden, " +
                $"{assembly.Failed} fehlgeschlagen, {assembly.LoadError} Ladefehler, {assembly.Skipped} übersprungen");
        }

        var total = overview.Total;
        System.Console.WriteLine(
            $"Gesamt: {total.Total} gesamt, {total.Passed} bestanden, {total.Failed} fehlgeschlagen, " +
            $"{total.LoadError} Ladefehler, {total.Skipped} übersprungen");

        foreach (var warning in overview.Warnings)
        {
            System.Console.WriteLine($"Warnung [{warning.RunName}]: {warning.Message}");
        }

        foreach (var fileFailure in overview.FileFailures)
        {
            System.Console.WriteLine($"Datei nicht lesbar [{fileFailure.FilePath}]: {fileFailure.Detail}");
        }

        foreach (var unresolvedInput in inputResolution.UnresolvedInputs)
        {
            System.Console.WriteLine($"Eingabe nicht aufgelöst [{unresolvedInput.RawInput}]: {unresolvedInput.Reason}");
        }

        System.Console.WriteLine(verdict.IsSuccessful ? "URTEIL: GRUEN" : "URTEIL: ROT");
        foreach (var reason in verdict.Reasons)
        {
            System.Console.WriteLine($"  - {reason.Message}");
        }
    }
}
