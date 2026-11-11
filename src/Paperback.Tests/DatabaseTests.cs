using System;
using System.IO;
using System.Threading.Tasks;
using Paperback.Core.Models;
using Paperback.Services.Database;
using Xunit;

namespace Paperback.Tests;

public class DatabaseTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteDatabaseService _dbService;

    public DatabaseTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"paperback_test_{Guid.NewGuid():N}.db");
        _dbService = new SqliteDatabaseService(_testDbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task InitializeAsync_CreatesTablesSuccessfully()
    {
        await _dbService.InitializeAsync();
        var books = await _dbService.GetAllBooksAsync();
        Assert.Empty(books);
    }

    [Fact]
    public async Task InsertAndGetBook_PersistsCorrectly()
    {
        await _dbService.InitializeAsync();

        var book = new Book
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = "The Great Gatsby",
            Author = "F. Scott Fitzgerald",
            FilePath = "C:\\Books\\gatsby.epub",
            FileHash = "TESTHASH123",
            Format = "EPUB",
            TotalPages = 180,
            CurrentPage = 25,
            ProgressPercent = 13.8,
            DateAdded = DateTime.UtcNow,
            LastOpened = DateTime.UtcNow
        };

        await _dbService.InsertBookAsync(book);

        var retrieved = await _dbService.GetBookByIdAsync(book.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("The Great Gatsby", retrieved.Title);
        Assert.Equal("F. Scott Fitzgerald", retrieved.Author);
        Assert.Equal("TESTHASH123", retrieved.FileHash);
        Assert.Equal(180, retrieved.TotalPages);
        Assert.Equal(25, retrieved.CurrentPage);

        var byHash = await _dbService.GetBookByHashAsync("TESTHASH123");
        Assert.NotNull(byHash);
        Assert.Equal(book.Id, byHash.Id);
    }

    [Fact]
    public async Task UpdateBookProgress_UpdatesProperly()
    {
        await _dbService.InitializeAsync();

        var book = new Book
        {
            Id = "b1",
            Title = "Test Book",
            Author = "Author",
            FilePath = "C:\\test.epub",
            FileHash = "HASHB1",
            Format = "EPUB",
            TotalPages = 100,
            CurrentPage = 1,
            ProgressPercent = 1.0
        };

        await _dbService.InsertBookAsync(book);
        await _dbService.UpdateBookProgressAsync("b1", 50, 100, 50.0, "epubcfi(/6/4[chap1]!/4/2)");

        var updated = await _dbService.GetBookByIdAsync("b1");
        Assert.NotNull(updated);
        Assert.Equal(50, updated.CurrentPage);
        Assert.Equal(50.0, updated.ProgressPercent);
        Assert.Equal("epubcfi(/6/4[chap1]!/4/2)", updated.LastCfiLocation);
    }

    [Fact]
    public async Task BookmarksAndHighlights_CRUD_Works()
    {
        await _dbService.InitializeAsync();

        var book = new Book
        {
            Id = "book_annot",
            Title = "Annotated Book",
            FilePath = "C:\\annot.epub",
            FileHash = "HASH_ANNOT"
        };
        await _dbService.InsertBookAsync(book);

        var bookmark = new Bookmark
        {
            Id = "bm1",
            BookId = "book_annot",
            PageIndex = 14,
            Title = "Chapter 2 Beginning"
        };
        await _dbService.AddBookmarkAsync(bookmark);

        var bookmarks = await _dbService.GetBookmarksAsync("book_annot");
        Assert.Single(bookmarks);
        Assert.Equal("Chapter 2 Beginning", bookmarks[0].Title);

        var highlight = new Highlight
        {
            Id = "hl1",
            BookId = "book_annot",
            SelectedText = "It was the best of times, it was the worst of times.",
            Color = "#FFE066",
            Note = "Famous opening line",
            Location = "p14"
        };
        await _dbService.AddHighlightAsync(highlight);

        var highlights = await _dbService.GetHighlightsAsync("book_annot");
        Assert.Single(highlights);
        Assert.Equal("Famous opening line", highlights[0].Note);

        await _dbService.DeleteBookmarkAsync("bm1");
        Assert.Empty(await _dbService.GetBookmarksAsync("book_annot"));
    }
}
