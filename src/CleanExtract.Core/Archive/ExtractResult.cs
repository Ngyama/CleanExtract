namespace CleanExtract.Core.Archive;

public sealed class ExtractResult
{
    public required string OutputDirectory { get; init; }

    public required int ExitCode { get; init; }

    public required bool Succeeded { get; init; }

    public string? Warning { get; init; }
}
