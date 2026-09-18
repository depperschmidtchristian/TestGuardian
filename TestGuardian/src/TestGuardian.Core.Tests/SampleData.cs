namespace TestGuardian.Core.Tests;

/// <summary>
/// Resolves paths into the repo's shared "Fixtures/Sample Data" folder from wherever the
/// test assembly happens to run, instead of duplicating the sample .trx files per project.
/// </summary>
internal static class SampleData
{
    public static string PathTo(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                "Could not locate the repository root (src.sln) from the test output directory.");
        }

        // directory now points at ".../TestGuardian/src"; the repo root is two levels up.
        var repoRoot = directory.Parent!.Parent!;
        return Path.Combine(repoRoot.FullName, "Fixtures", "Sample Data", fileName);
    }
}
