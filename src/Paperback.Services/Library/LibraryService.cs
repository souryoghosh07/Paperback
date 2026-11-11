using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Paperback.Core.Interfaces;
using Paperback.Core.Models;

namespace Paperback.Services.Library;

public class LibraryService : ILibraryService
{
    private readonly IDatabaseService _databaseService;
    private readonly IEpubService _epubService;
    private readonly IPdfService _pdfService;
    private readonly string _coversDirectory;

    public ObservableCollection<Book> Books { get; } = new();

    public LibraryService(
        IDatabaseService databaseService,
        IEpubService epubService,
        IPdfService pdfService,
        string? coversDirectory = null)
    {
        _databaseService = databaseService;
        _epubService = epubService;
        _pdfService = pdfService;

        if (!string.IsNullOrWhiteSpace(coversDirectory))
        {
            _coversDirectory = coversDirectory;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _coversDirectory = Path.Combine(appData, "Paperback", "covers");
        }
        Directory.CreateDirectory(_coversDirectory);
    }

    public async Task InitializeAsync()
    {
        await _databaseService.InitializeAsync();
        var allBooks = await _databaseService.GetAllBooksAsync();
        
        Books.Clear();
        foreach (var book in allBooks)
        {
            // Verify if cover exists
            if (!string.IsNullOrEmpty(book.CoverImagePath) && !File.Exists(book.CoverImagePath))
            {
                book.CoverImagePath = null;
            }
            Books.Add(book);
        }
    }

    public async Task<Book?> ImportBookAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext != ".epub" && ext != ".pdf")
        {
            return null;
        }

        string hash = await ComputeFileHashAsync(filePath);

        // Check if already in library
        var existing = await _databaseService.GetBookByHashAsync(hash);
        if (existing != null)
        {
            // Update file path if moved
            if (existing.FilePath != filePath)
            {
                existing.FilePath = filePath;
                await _databaseService.InsertBookAsync(existing);
            }
            return existing;
        }

        var book = new Book
        {
            Id = Guid.NewGuid().ToString("N"),
            FilePath = filePath,
            FileHash = hash,
            Format = ext == ".epub" ? "EPUB" : "PDF",
            DateAdded = DateTime.UtcNow,
            LastOpened = DateTime.UtcNow
        };

        if (book.Format == "EPUB")
        {
            var meta = await _epubService.ExtractMetadataAsync(filePath);
            book.Title = meta.Title;
            book.Author = meta.Author;
            book.Description = meta.Description;
            book.TotalPages = meta.EstimatedPageCount;

            string coverPath = await _epubService.EnsureCoverExtractedAsync(book.Id, filePath, _coversDirectory);
            if (!string.IsNullOrEmpty(coverPath) && File.Exists(coverPath))
            {
                book.CoverImagePath = coverPath;
            }
        }
        else
        {
            var meta = await _pdfService.ExtractMetadataAsync(filePath);
            book.Title = meta.Title;
            book.Author = meta.Author;
            book.TotalPages = meta.PageCount;

            string coverPath = await _pdfService.EnsureCoverExtractedAsync(book.Id, filePath, _coversDirectory);
            if (!string.IsNullOrEmpty(coverPath) && File.Exists(coverPath))
            {
                book.CoverImagePath = coverPath;
            }
        }

        await _databaseService.InsertBookAsync(book);

        // Add to in-memory list (at the top)
        Books.Insert(0, book);
        return book;
    }

    public async Task<int> ImportDirectoryAsync(string directoryPath, bool recursive = true)
    {
        if (!Directory.Exists(directoryPath))
        {
            return 0;
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(directoryPath, "*.*", searchOption)
            .Where(f => f.EndsWith(".epub", StringComparison.OrdinalIgnoreCase) || 
                        f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .ToList();

        int importedCount = 0;
        foreach (var file in files)
        {
            try
            {
                var book = await ImportBookAsync(file);
                if (book != null)
                {
                    importedCount++;
                }
            }
            catch
            {
                // Continue on individual file errors
            }
        }

        return importedCount;
    }

    public async Task UpdateReadingProgressAsync(string bookId, int currentPage, int totalPages, double progressPercent, string? lastCfi)
    {
        await _databaseService.UpdateBookProgressAsync(bookId, currentPage, totalPages, progressPercent, lastCfi);

        var book = Books.FirstOrDefault(b => b.Id == bookId);
        if (book != null)
        {
            book.CurrentPage = currentPage;
            book.TotalPages = totalPages > 0 ? totalPages : book.TotalPages;
            book.ProgressPercent = progressPercent;
            book.LastCfiLocation = lastCfi;
            book.LastOpened = DateTime.UtcNow;
        }
    }

    public async Task MarkAsFinishedAsync(string bookId)
    {
        var book = Books.FirstOrDefault(b => b.Id == bookId);
        int total = book?.TotalPages ?? 1;
        await UpdateReadingProgressAsync(bookId, total, total, 100.0, book?.LastCfiLocation);
    }

    public async Task RemoveBookAsync(string bookId, bool deleteFileFromDisk = false)
    {
        var book = Books.FirstOrDefault(b => b.Id == bookId);
        if (book != null)
        {
            Books.Remove(book);

            if (!string.IsNullOrEmpty(book.CoverImagePath) && File.Exists(book.CoverImagePath))
            {
                try { File.Delete(book.CoverImagePath); } catch { }
            }

            if (deleteFileFromDisk && File.Exists(book.FilePath))
            {
                try { File.Delete(book.FilePath); } catch { }
            }
        }

        await _databaseService.DeleteBookAsync(bookId);
    }

    public Task<List<Book>> FilterBooksAsync(string category, string searchQuery = "", string sortBy = "Recent")
    {
        var query = Books.AsEnumerable();

        // 1. Filter by category
        switch (category.ToLowerInvariant())
        {
            case "reading now":
            case "current":
                query = query.Where(b => b.ProgressPercent > 0 && b.ProgressPercent < 99.0);
                break;
            case "finished":
                query = query.Where(b => b.ProgressPercent >= 99.0);
                break;
            case "want to read":
            case "unread":
                query = query.Where(b => b.ProgressPercent == 0.0);
                break;
            case "epubs":
            case "epub":
                query = query.Where(b => b.Format.Equals("EPUB", StringComparison.OrdinalIgnoreCase));
                break;
            case "pdfs":
            case "pdf":
                query = query.Where(b => b.Format.Equals("PDF", StringComparison.OrdinalIgnoreCase));
                break;
            case "all books":
            default:
                break;
        }

        // 2. Search filter
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            string term = searchQuery.Trim();
            query = query.Where(b => 
                b.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                b.Author.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        // 3. Sorting
        query = sortBy switch
        {
            "Title" => query.OrderBy(b => b.Title),
            "Author" => query.OrderBy(b => b.Author),
            "Date Added" => query.OrderByDescending(b => b.DateAdded),
            "Progress" => query.OrderByDescending(b => b.ProgressPercent),
            _ => query.OrderByDescending(b => b.LastOpened) // "Recent" default
        };

        return Task.FromResult(query.ToList());
    }

    private static async Task<string> ComputeFileHashAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        
        // Hash first 1MB + last 1MB + file length for speed on large PDFs
        if (stream.Length > 2 * 1024 * 1024)
        {
            byte[] header = new byte[1024 * 1024];
            int read = await stream.ReadAsync(header.AsMemory(0, header.Length));
            sha256.TransformBlock(header, 0, read, null, 0);

            stream.Seek(-1024 * 1024, SeekOrigin.End);
            byte[] footer = new byte[1024 * 1024];
            read = await stream.ReadAsync(footer.AsMemory(0, footer.Length));
            sha256.TransformBlock(footer, 0, read, null, 0);

            byte[] lengthBytes = BitConverter.GetBytes(stream.Length);
            sha256.TransformFinalBlock(lengthBytes, 0, lengthBytes.Length);
            return Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>());
        }

        byte[] hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hash);
    }
}
