using System;
using System.IO;
using System.Threading.Tasks;
using Paperback.Services.Database;
using Paperback.Services.Epub;
using Paperback.Services.Library;
using Paperback.Services.Pdf;
using Xunit;

namespace Paperback.Tests;

public class SampleIngestionTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _testCoversDir;
    private readonly SqliteDatabaseService _dbService;
    private readonly VersOneEpubService _epubService;
    private readonly WindowsPdfService _pdfService;
    private readonly LibraryService _libraryService;

    public SampleIngestionTests()
    {
        string temp = Path.GetTempPath();
        _testDbPath = Path.Combine(temp, $"paperback_sample_db_{Guid.NewGuid():N}.db");
        _testCoversDir = Path.Combine(temp, $"paperback_sample_covers_{Guid.NewGuid():N}");
        _dbService = new SqliteDatabaseService(_testDbPath);
        _epubService = new VersOneEpubService();
        _pdfService = new WindowsPdfService();
        _libraryService = new LibraryService(_dbService, _epubService, _pdfService, _testCoversDir);
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath)) { try { File.Delete(_testDbPath); } catch { } }
        if (Directory.Exists(_testCoversDir)) { try { Directory.Delete(_testCoversDir, true); } catch { } }
    }

    private static string GetSamplesDirectory()
    {
        string dir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "samples"));
        if (!Directory.Exists(dir))
        {
            dir = @"C:\Users\soury\Documents\Personal\Projects\Paperback\samples";
        }
        return dir;
    }

    [Fact]
    public async Task ExtractEpubMetadata_RealEpub_Succeeds()
    {
        string samplesDir = GetSamplesDirectory();
        string epubPath = Path.Combine(samplesDir, "Sherlock_Holmes_A_Scandal_in_Bohemia.epub");
        EpubGenerator.GenerateSampleEpub(epubPath);
        Assert.True(File.Exists(epubPath), "Sample EPUB should exist");

        var meta = await _epubService.ExtractMetadataAsync(epubPath);
        Assert.Equal("A Scandal in Bohemia", meta.Title);
        Assert.Equal("Arthur Conan Doyle", meta.Author);
        Assert.Contains("Sherlock Holmes", meta.Description);

        var toc = await _epubService.GetTableOfContentsAsync(epubPath);
        Assert.Equal(2, toc.Count);
        Assert.Equal("Chapter I: The Arrival", toc[0].Title);
        Assert.Equal("Chapter II: The Photograph", toc[1].Title);
    }

    [Fact]
    public async Task ExtractPdfMetadata_RealPdf_Succeeds()
    {
        string samplesDir = GetSamplesDirectory();
        string pdfPath = Path.Combine(samplesDir, "Design_Systems_Guide.pdf");
        Assert.True(File.Exists(pdfPath), "Sample PDF should exist");

        var meta = await _pdfService.ExtractMetadataAsync(pdfPath);
        Assert.Equal("Design Systems and Typography on Modern Windows", meta.Title);
        Assert.Equal("Design Guild", meta.Author);
        Assert.Equal(2, meta.PageCount);
    }

    [Fact]
    public async Task IngestSamplesIntoLibrary_IntegratesSuccessfully()
    {
        await _libraryService.InitializeAsync();
        string samplesDir = GetSamplesDirectory();

        int imported = await _libraryService.ImportDirectoryAsync(samplesDir, recursive: false);
        Assert.Equal(2, imported);
        Assert.Equal(2, _libraryService.Books.Count);

        // Verify duplicate ingestion returns existing book and does not add duplicate
        int reimported = await _libraryService.ImportDirectoryAsync(samplesDir, recursive: false);
        Assert.Equal(2, reimported);
        Assert.Equal(2, _libraryService.Books.Count);
    }
}
