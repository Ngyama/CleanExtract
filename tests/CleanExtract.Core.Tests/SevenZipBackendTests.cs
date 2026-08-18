using System.IO.Compression;
using System.Text;
using CleanExtract.Core.Archive;
using CleanExtract.Core.Cleaning;
using CleanExtract.Core.Logging;
using CleanExtract.Core.SevenZip;
using CleanExtract.Core.Workflow;

namespace CleanExtract.Core.Tests;

public sealed class SevenZipBackendTests
{
    private static string? FindSevenZip()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "resources", "7zz.exe");
        return File.Exists(path) ? path : null;
    }

    [Fact]
    public async Task ListsAndExtracts_SkippingTrash()
    {
        var sevenZip = FindSevenZip();
        if (sevenZip is null)
            return;

        var work = Directory.CreateTempSubdirectory("cleanextract-it-");
        try
        {
            var archivePath = Path.Combine(work.FullName, "sample.zip");
            CreateSampleZip(archivePath);

            var backend = new SevenZipBackend(sevenZip, NullLog.Instance);
            var entries = await backend.ListEntriesAsync(archivePath, password: null);

            Assert.Contains(entries, e => e.FileName == "game.exe");
            Assert.Contains(entries, e => e.FileName.Contains("本站最新网址"));
            Assert.Contains(entries, e => e.FileName == "説明.txt" || e.Path.Contains("日本語"));
            Assert.Contains(entries, e => e.FileName == ".DS_Store");

            var engine = new CleanerEngine();
            var contents = new Dictionary<string, EntryContent>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries.Where(e => engine.NeedsContent(e)))
            {
                var bytes = await backend.ReadEntryAsync(archivePath, entry, password: null);
                contents[entry.Path] = EntryContent.FromBytes(bytes);
            }

            var verdicts = engine.Classify(entries, contents);
            var trash = verdicts.Where(v => v.Classification == Classification.Trash).Select(v => v.Entry.Path).ToList();
            Assert.Contains(trash, p => p.Contains(".DS_Store", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(trash, p => p.Contains("本站最新网址"));

            var output = Path.Combine(work.FullName, "out");
            var result = await backend.ExtractAsync(archivePath, output, trash, password: null);
            Assert.True(result.Succeeded);

            Assert.True(File.Exists(Path.Combine(output, "Game", "game.exe")));
            Assert.True(File.Exists(Path.Combine(output, "日本語", "説明.txt")));
            Assert.False(File.Exists(Path.Combine(output, ".DS_Store")));
            Assert.DoesNotContain(Directory.GetFiles(output, "*", SearchOption.AllDirectories),
                f => Path.GetFileName(f).Contains("本站最新网址"));
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task PasswordProtectedZip_ExtractsAfterPassword()
    {
        var sevenZip = FindSevenZip();
        if (sevenZip is null)
            return;

        var work = Directory.CreateTempSubdirectory("cleanextract-pw-");
        try
        {
            var payload = Path.Combine(work.FullName, "secret.txt");
            File.WriteAllText(payload, "ok");
            var archivePath = Path.Combine(work.FullName, "secret.zip");

            var packer = new SevenZipBackend(sevenZip, NullLog.Instance);
            var create = await RunHelper.CreateZipWithPassword(sevenZip, archivePath, payload, "s3cret");
            Assert.True(create, "7-Zip should create a password-protected zip");

            var backend = new SevenZipBackend(sevenZip, NullLog.Instance);
            await Assert.ThrowsAnyAsync<PasswordRequiredException>(async () =>
            {
                var listed = await backend.ListEntriesAsync(archivePath, password: null);
                var encrypted = listed.First(e => !e.IsDirectory);
                await backend.ReadEntryAsync(archivePath, encrypted, password: null);
            });

            var entries = await backend.ListEntriesAsync(archivePath, "s3cret");
            Assert.Contains(entries, e => e.FileName == "secret.txt");

            var output = Path.Combine(work.FullName, "out");
            var extracted = await backend.ExtractAsync(archivePath, output, [], "s3cret");
            Assert.True(extracted.Succeeded);
            Assert.True(File.Exists(Path.Combine(output, "secret.txt")));
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Workflow_FiltersAdsWithoutModifyingArchive()
    {
        var sevenZip = FindSevenZip();
        if (sevenZip is null)
            return;

        var work = Directory.CreateTempSubdirectory("cleanextract-wf-");
        try
        {
            var archivePath = Path.Combine(work.FullName, "pack.zip");
            CreateSampleZip(archivePath);
            var originalHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(archivePath)));

            var backend = new SevenZipBackend(sevenZip, NullLog.Instance);
            var workflow = new CleanExtractWorkflow(backend, new CleanerEngine(), new NoPassword(), NullLog.Instance);
            var result = await workflow.RunAsync(archivePath);

            Assert.True(Directory.Exists(result.OutputDirectory));
            Assert.True(result.Summary.TrashCount >= 2);
            Assert.True(File.Exists(Path.Combine(result.OutputDirectory, "Game", "game.exe")));
            var afterHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(archivePath)));
            Assert.Equal(originalHash, afterHash);
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    private static void CreateSampleZip(string archivePath)
    {
        using var stream = File.Create(archivePath);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8);
        Write(zip, "Game/game.exe", "MZ-fake"u8.ToArray());
        Write(zip, "README.txt", "hello"u8.ToArray());
        Write(zip, "日本語/説明.txt", "日本語テスト"u8.ToArray());
        Write(zip, "emoji 😀/hello world.pak", "data"u8.ToArray());
        Write(zip, ".DS_Store", "junk"u8.ToArray());
        Write(zip, "★本站最新网址.url", """
            [InternetShortcut]
            URL=https://example.invalid/ad
            """u8.ToArray());
        Write(zip, "Documentation.url", """
            [InternetShortcut]
            URL=https://github.com/example/project
            """u8.ToArray());
        Write(zip, "使用说明.txt", "安装步骤"u8.ToArray());
    }

    private static void Write(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private sealed class NoPassword : IPasswordPrompt
    {
        public Task<string?> RequestPasswordAsync(string archivePath, bool previousWasWrong, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);
    }
}

internal static class RunHelper
{
    public static async Task<bool> CreateZipWithPassword(string sevenZip, string archivePath, string filePath, string password)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = sevenZip,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("a");
        psi.ArgumentList.Add("-tzip");
        psi.ArgumentList.Add("-p" + password);
        psi.ArgumentList.Add(archivePath);
        psi.ArgumentList.Add(filePath);
        using var process = System.Diagnostics.Process.Start(psi);
        if (process is null)
            return false;
        await process.WaitForExitAsync();
        return process.ExitCode == 0 && File.Exists(archivePath);
    }
}
