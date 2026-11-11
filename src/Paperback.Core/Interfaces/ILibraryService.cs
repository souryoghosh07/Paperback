using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Paperback.Core.Models;

namespace Paperback.Core.Interfaces;

public interface ILibraryService
{
    ObservableCollection<Book> Books { get; }
    Task InitializeAsync();
    Task<Book?> ImportBookAsync(string filePath);
    Task<int> ImportDirectoryAsync(string directoryPath, bool recursive = true);
    Task UpdateReadingProgressAsync(string bookId, int currentPage, int totalPages, double progressPercent, string? lastCfi);
    Task MarkAsFinishedAsync(string bookId);
    Task RemoveBookAsync(string bookId, bool deleteFileFromDisk = false);
    Task<List<Book>> FilterBooksAsync(string category, string searchQuery = "", string sortBy = "Recent");
}
