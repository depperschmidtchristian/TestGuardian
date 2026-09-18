namespace TestGuardian.Core.Trx;

public enum TrxReadFailureReason
{
    FileNotFound,
    EmptyFile,
    MalformedXml
}

/// <summary>
/// <see cref="FilePath"/> is null when a failure originates from
/// <see cref="TrxDocumentParser.Parse"/>, which only sees XML content, not a path.
/// <see cref="TrxFileReader"/> fills it in once the path is known.
/// </summary>
public sealed record TrxReadFailure(string? FilePath, TrxReadFailureReason Reason, string Detail);
