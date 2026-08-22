using CleanExtract.Core.Archive;
using CleanExtract.Core.Cleaning;
using CleanExtract.Core.Logging;
using CleanExtract.Core.Workflow;

namespace CleanExtract.Core.Tests;

public sealed class WorkflowTests
{
    [Fact]
    public async Task AnalyzeThenExtract_SkipsTrash_AndCanExtractInPlace()
    {
        var work = Directory.CreateTempSubdirectory("cleanextract-plan-");
        try
        {
            var archive = Path.Combine(work.FullName, "Game.zip");
            File.WriteAllBytes(archive, [1, 2, 3]);

            var backend = CreateBackend();
            var workflow = new CleanExtractWorkflow(backend, new CleanerEngine(), new NoPassword(), NullLog.Instance);

            var plan = await workflow.AnalyzeAsync(archive);
            Assert.Contains(plan.Verdicts, v => v.Entry.FileName == ".DS_Store" && v.Classification == Classification.Trash);
            Assert.Contains(plan.ExcludedPaths(keepSuspicious: true), p => p.Contains("本站最新网址"));
            Assert.Contains(plan.Verdicts, v => v.Entry.FileName == "game.exe" && v.Classification == Classification.Clean);

            var result = await workflow.ExtractAsync(plan, work.FullName, uniquifyOutput: false);
            Assert.Equal(work.FullName, result.OutputDirectory);
            Assert.Equal(work.FullName, backend.LastOutput);
            Assert.Contains(backend.LastExcluded!, p => p.Contains(".DS_Store", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(backend.LastExcluded!, p => p.Contains("本站最新网址"));
            Assert.DoesNotContain(backend.LastExcluded!, p => p.Contains("game.exe"));
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Recategorize_HonorsAlwaysKeepOverride()
    {
        var work = Directory.CreateTempSubdirectory("cleanextract-keep-");
        try
        {
            var archive = Path.Combine(work.FullName, "Game.zip");
            File.WriteAllBytes(archive, [1, 2, 3]);
            var backend = CreateBackend();
            var engine = new CleanerEngine();
            var workflow = new CleanExtractWorkflow(backend, engine, new NoPassword(), NullLog.Instance);

            var plan = await workflow.AnalyzeAsync(archive);
            Assert.Contains(plan.ExcludedPaths(true), p => p.Contains("本站最新网址"));

            engine.Config.AlwaysKeepNames.Add("★本站最新网址.url");
            var updated = workflow.Recategorize(plan);
            Assert.DoesNotContain(updated.ExcludedPaths(true), p => p.Contains("本站最新网址"));
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    private static FakeArchiveBackend CreateBackend()
    {
        var backend = new FakeArchiveBackend();
        backend.Entries.Add(Entries.File("Game/game.exe", 8_000_000));
        backend.Entries.Add(Entries.File("README.txt", 200));
        backend.Entries.Add(Entries.File(".DS_Store", 6));
        backend.Entries.Add(Entries.File("★本站最新网址.url", 90));
        backend.Content["★本站最新网址.url"] = """
            [InternetShortcut]
            URL=https://example.invalid/ad
            """u8.ToArray();
        return backend;
    }

    private sealed class FakeArchiveBackend : IArchiveBackend
    {
        public List<ArchiveEntry> Entries { get; } = [];
        public Dictionary<string, byte[]> Content { get; } = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<string>? LastExcluded { get; private set; }
        public string? LastOutput { get; private set; }

        public Task<IReadOnlyList<ArchiveEntry>> ListEntriesAsync(
            string archivePath,
            string? password,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ArchiveEntry>>(Entries);

        public Task<byte[]> ReadEntryAsync(
            string archivePath,
            ArchiveEntry entry,
            string? password,
            CancellationToken cancellationToken = default)
        {
            Content.TryGetValue(entry.Path, out var bytes);
            return Task.FromResult(bytes ?? []);
        }

        public Task<ExtractResult> ExtractAsync(
            string archivePath,
            string outputDirectory,
            IReadOnlyList<string> excludedPaths,
            string? password,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            LastOutput = outputDirectory;
            LastExcluded = excludedPaths.ToList();
            return Task.FromResult(new ExtractResult
            {
                OutputDirectory = outputDirectory,
                ExitCode = 0,
                Succeeded = true,
            });
        }
    }

    private sealed class NoPassword : IPasswordPrompt
    {
        public Task<string?> RequestPasswordAsync(string archivePath, bool previousWasWrong, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);
    }
}
