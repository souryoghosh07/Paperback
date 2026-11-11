using System;
using System.IO;
using System.Threading.Tasks;
using Paperback.Core.Models;
using Paperback.Services.Database;
using Paperback.Services.Epub;
using Paperback.Services.Library;
using Paperback.Services.Pdf;
using Xunit;

namespace Paperback.Tests;

public class LibraryServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _testCoversDir;
    private readonly SqliteDatabaseService _dbService;
    private readonly LibraryService _libraryService;

    public LibraryServiceTests()
    {
        string temp = Path.GetTempPath();
        _testDbPath = Path.Combine(temp, $"paperback_lib_{Guid.NewGuid():N}.db");
        _testCoversDir = Path.Combine(temp, $"paperback_covers_{Guid.NewGuid():N}");
        _dbService = new SqliteDatabaseService(_testDbPath);
        _libraryService = new LibraryService(_dbService, new VersOneEpubService(), new WindowsPdfService(), _testCoversDir);
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath)) { try { File.Delete(_testDbPath); } catch { } }
        if (Directory.Exists(_testCoversDir)) { try { Directory.Delete(_testCoversDir, true); } catch { } }
    }

    [Fact]
    public async Task FilteringAndSorting_WorksAsExpected()
    {
        await _libraryService.InitializeAsync();

        var b1 = new Book
        {
            Id = "1",
            Title = "Dune",
            Author = "Frank Herbert",
            Format = "EPUB",
            ProgressPercent = 45.0,
            DateAdded = DateTime.UtcNow.AddDays(-5),
            LastOpened = DateTime.UtcNow.AddHours(-1)
        };
        var b2 = new Book
        {
            Id = "2",
            Title = "Clean Code",
            Author = "Robert C. Martin",
            Format = "PDF",
            ProgressPercent = 100.0,
            DateAdded = DateTime.UtcNow.AddDays(-10),
            LastOpened = DateTime.UtcNow.AddDays(-2)
        };
        var b3 = new Book
        {
            Id = "3",
            Title = "Neuromancer",
            Author = "William Gibson",
            Format = "EPUB",
            ProgressPercent = 0.0,
            DateAdded = DateTime.UtcNow.AddDays(-1),
            LastOpened = DateTime.UtcNow.AddDays(-1)
        };

        _libraryService.Books.Add(b1);
        _libraryService.Books.Add(b2);
        _libraryService.Books.Add(b3);

        // Filter: Reading Now
        var readingNow = await _libraryService.FilterBooksAsync("Reading Now");
        Assert.Single(readingNow);
        Assert.Equal("Dune", readingNow[0].Title);

        // Filter: Finished
        var finished = await _libraryService.FilterBooksAsync("Finished");
        Assert.Single(finished);
        Assert.Equal("Clean Code", finished[0].Title);

        // Filter: EPUBs
        var epubs = await _libraryService.FilterBooksAsync("EPUBs");
        Assert.Equal(2, epubs.Count);

        // Filter: PDFs
        var pdfs = await _libraryService.FilterBooksAsync("PDFs");
        Assert.Single(pdfs);
        Assert.Equal("Clean Code", pdfs[0].Title);

        // Search: "gibson"
        var searchResult = await _libraryService.FilterBooksAsync("All Books", "gibson");
        Assert.Single(searchResult);
        Assert.Equal("Neuromancer", searchResult[0].Title);

        // Sort by Title
        var sortedByTitle = await _libraryService.FilterBooksAsync("All Books", "", "Title");
        Assert.Equal("Clean Code", sortedByTitle[0].Title);
        Assert.Equal("Dune", sortedByTitle[1].Title);
        Assert.Equal("Neuromancer", sortedByTitle[2].Title);
    }
}
