using TestGuardian.Core.Allowlist.Models;

namespace TestGuardian.Core.Allowlist;

public static class KnownFailureAllowlistLoader
{
    public static KnownFailureAllowlistLoadResult Load(string path)
    {
        if (!File.Exists(path))
        {
            return new KnownFailureAllowlistLoadResult.Failure($"Die Allowlist-Datei '{path}' existiert nicht.");
        }

        return new KnownFailureAllowlistLoadResult.Success(KnownFailureAllowlist.Parse(File.ReadAllLines(path)));
    }
}
