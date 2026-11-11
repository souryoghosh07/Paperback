using System.Collections.Generic;
using System.Threading.Tasks;
using Paperback.Core.Models;

namespace Paperback.Core.Interfaces;

public interface IDatabaseService
{
    Task InitializeAsync();
    Task<List<Book>> GetAllBooksAsync();
    Task<Book?> GetBookByIdAsync(string id);
    Task<Book?> GetBookByHashAsync(string fileHash);
    Task InsertBookAsync(Book book);
    Task UpdateBookProgressAsync(string bookId, int currentPage, int totalPages, double progressPercent, string? lastCfi);
    Task UpdateBookLastOpenedAsync(string bookId);
    Task DeleteBookAsync(string bookId);

    Task<List<Bookmark>> GetBookmarksAsync(string bookId);
    Task AddBookmarkAsync(Bookmark bookmark);
    Task DeleteBookmarkAsync(string bookmarkId);

    Task<List<Highlight>> GetHighlightsAsync(string bookId);
    Task AddHighlightAsync(Highlight highlight);
    Task DeleteHighlightAsync(string highlightId);

    Task<List<Collection>> GetCollectionsAsync();
    Task CreateCollectionAsync(Collection collection);
    Task AddBookToCollectionAsync(string collectionId, string bookId);
    Task RemoveBookFromCollectionAsync(string collectionId, string bookId);
}
