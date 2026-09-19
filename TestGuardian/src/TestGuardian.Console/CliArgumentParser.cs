namespace TestGuardian.Console;

public sealed record CliOptions(IReadOnlyList<string> Inputs, int MaxDepth, int? MinTests);

/// <summary>
/// Hand-rolled instead of a library like System.CommandLine — see docs/decisions/exit-code-decision.md
/// for why: the option surface is small and the three-state --max-depth semantics (absent/no
/// value/explicit value) is a special case most generic parsers don't cover directly anyway.
/// </summary>
public static class CliArgumentParser
{
    private const int DefaultMaxDepthWhenSwitchGivenWithoutValue = 10;

    public static CliOptions Parse(string[] args)
    {
        var inputs = new List<string>();
        var maxDepth = 0; // switch entirely absent -> no recursion, see docs/plans/teil-a-overview.md
        int? minTests = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--max-depth" when i + 1 < args.Length && int.TryParse(args[i + 1], out var explicitDepth):
                    maxDepth = explicitDepth;
                    i++;
                    break;
                case "--max-depth":
                    maxDepth = DefaultMaxDepthWhenSwitchGivenWithoutValue;
                    break;
                case "--min-tests" when i + 1 < args.Length && int.TryParse(args[i + 1], out var explicitMin):
                    minTests = explicitMin;
                    i++;
                    break;
                case "--min-tests":
                    throw new ArgumentException("--min-tests erwartet eine ganze Zahl als Wert.");
                case var unknownOption when unknownOption.StartsWith("--", StringComparison.Ordinal):
                    throw new ArgumentException(
                        $"Unbekannte Option '{unknownOption}'. Ein Tippfehler in einem Schalter darf nicht " +
                        "stillschweigend als zu prüfende Datei behandelt werden.");
                default:
                    inputs.Add(args[i]);
                    break;
            }
        }

        if (inputs.Count == 0)
        {
            // No positional input at all means TrxInputResolver never even attempts to resolve
            // anything, so neither ResolvedFilePaths nor UnresolvedInputs ends up non-empty — nothing
            // downstream would flag this as a problem otherwise (see docs/decisions/input-warning-level.md).
            throw new ArgumentException(
                "Keine Eingabe angegeben. Bitte mindestens eine .trx-Datei, einen Ordner oder ein Suchmuster angeben.");
        }

        return new CliOptions(inputs, maxDepth, minTests);
    }
}
