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
                default:
                    inputs.Add(args[i]);
                    break;
            }
        }

        return new CliOptions(inputs, maxDepth, minTests);
    }
}
