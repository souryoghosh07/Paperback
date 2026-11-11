using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paperback.Core.Interfaces;
using Paperback.Core.Models;

namespace Paperback.Services.Database;

public class SqliteDatabaseService : IDatabaseService
{
    private readonly string _connectionString;

    public SqliteDatabaseService(string? customDbPath = null)
    {
        string dbPath;
        if (!string.IsNullOrWhiteSpace(customDbPath))
        {
            dbPath = customDbPath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appData, "Paperback");
            Directory.CreateDirectory(appFolder);
            dbPath = Path.Combine(appFolder, "library.db");
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        _connectionString = builder.ToString();
    }

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string createTablesSql = @"
            CREATE TABLE IF NOT EXISTS Books (
                Id TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                Author TEXT NOT NULL,
                FilePath TEXT NOT NULL UNIQUE,
                FileHash TEXT NOT NULL,
                Format TEXT NOT NULL,
                CoverImagePath TEXT,
                Description TEXT,
                TotalPages INTEGER NOT NULL DEFAULT 1,
                CurrentPage INTEGER NOT NULL DEFAULT 1,
                ProgressPercent REAL NOT NULL DEFAULT 0.0,
                LastCfiLocation TEXT,
                DateAdded TEXT NOT NULL,
                LastOpened TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Bookmarks (
                Id TEXT PRIMARY KEY,
                BookId TEXT NOT NULL,
                PageIndex INTEGER NOT NULL,
                CfiLocation TEXT,
                Title TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY(BookId) REFERENCES Books(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Highlights (
                Id TEXT PRIMARY KEY,
                BookId TEXT NOT NULL,
                SelectedText TEXT NOT NULL,
                Color TEXT NOT NULL,
                Note TEXT,
                Location TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY(BookId) REFERENCES Books(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Collections (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL UNIQUE,
                Description TEXT,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS CollectionBooks (
                CollectionId TEXT NOT NULL,
                BookId TEXT NOT NULL,
                PRIMARY KEY (CollectionId, BookId),
                FOREIGN KEY(CollectionId) REFERENCES Collections(Id) ON DELETE CASCADE,
                FOREIGN KEY(BookId) REFERENCES Books(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_books_hash ON Books(FileHash);
            CREATE INDEX IF NOT EXISTS idx_books_last_opened ON Books(LastOpened DESC);
        ";

        using var command = new SqliteCommand(createTablesSql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<Book>> GetAllBooksAsync()
    {
        var result = new List<Book>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = "SELECT * FROM Books ORDER BY LastOpened DESC";
        using var command = new SqliteCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(ReadBook(reader));
        }
        return result;
    }

    public async Task<Book?> GetBookByIdAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = "SELECT * FROM Books WHERE Id = @Id LIMIT 1";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadBook(reader);
        }
        return null;
    }

    public async Task<Book?> GetBookByHashAsync(string fileHash)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = "SELECT * FROM Books WHERE FileHash = @Hash LIMIT 1";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Hash", fileHash);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadBook(reader);
        }
        return null;
    }

    public async Task InsertBookAsync(Book book)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = @"
            INSERT OR REPLACE INTO Books (
                Id, Title, Author, FilePath, FileHash, Format, CoverImagePath, Description,
                TotalPages, CurrentPage, ProgressPercent, LastCfiLocation, DateAdded, LastOpened
            ) VALUES (
                @Id, @Title, @Author, @FilePath, @FileHash, @Format, @CoverImagePath, @Description,
                @TotalPages, @CurrentPage, @ProgressPercent, @LastCfiLocation, @DateAdded, @LastOpened
            );
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", book.Id);
        command.Parameters.AddWithValue("@Title", book.Title);
        command.Parameters.AddWithValue("@Author", book.Author);
        command.Parameters.AddWithValue("@FilePath", book.FilePath);
        command.Parameters.AddWithValue("@FileHash", book.FileHash);
        command.Parameters.AddWithValue("@Format", book.Format);
        command.Parameters.AddWithValue("@CoverImagePath", (object?)book.CoverImagePath ?? DBNull.Value);
        command.Parameters.AddWithValue("@Description", (object?)book.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@TotalPages", book.TotalPages);
        command.Parameters.AddWithValue("@CurrentPage", book.CurrentPage);
        command.Parameters.AddWithValue("@ProgressPercent", book.ProgressPercent);
        command.Parameters.AddWithValue("@LastCfiLocation", (object?)book.LastCfiLocation ?? DBNull.Value);
        command.Parameters.AddWithValue("@DateAdded", book.DateAdded.ToString("O"));
        command.Parameters.AddWithValue("@LastOpened", book.LastOpened.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateBookProgressAsync(string bookId, int currentPage, int totalPages, double progressPercent, string? lastCfi)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = @"
            UPDATE Books SET
                CurrentPage = @CurrentPage,
                TotalPages = @TotalPages,
                ProgressPercent = @ProgressPercent,
                LastCfiLocation = @LastCfi,
                LastOpened = @LastOpened
            WHERE Id = @Id;
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@CurrentPage", currentPage);
        command.Parameters.AddWithValue("@TotalPages", totalPages > 0 ? totalPages : 1);
        command.Parameters.AddWithValue("@ProgressPercent", Math.Clamp(progressPercent, 0.0, 100.0));
        command.Parameters.AddWithValue("@LastCfi", (object?)lastCfi ?? DBNull.Value);
        command.Parameters.AddWithValue("@LastOpened", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@Id", bookId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateBookLastOpenedAsync(string bookId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = "UPDATE Books SET LastOpened = @LastOpened WHERE Id = @Id";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@LastOpened", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@Id", bookId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteBookAsync(string bookId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = "DELETE FROM Books WHERE Id = @Id";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", bookId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<Bookmark>> GetBookmarksAsync(string bookId)
    {
        var result = new List<Bookmark>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = "SELECT * FROM Bookmarks WHERE BookId = @BookId ORDER BY PageIndex ASC";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@BookId", bookId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new Bookmark
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                BookId = reader.GetString(reader.GetOrdinal("BookId")),
                PageIndex = reader.GetInt32(reader.GetOrdinal("PageIndex")),
                CfiLocation = reader.IsDBNull(reader.GetOrdinal("CfiLocation")) ? null : reader.GetString(reader.GetOrdinal("CfiLocation")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
            });
        }
        return result;
    }

    public async Task AddBookmarkAsync(Bookmark bookmark)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = @"
            INSERT OR REPLACE INTO Bookmarks (Id, BookId, PageIndex, CfiLocation, Title, CreatedAt)
            VALUES (@Id, @BookId, @PageIndex, @CfiLocation, @Title, @CreatedAt);
        ";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", bookmark.Id);
        command.Parameters.AddWithValue("@BookId", bookmark.BookId);
        command.Parameters.AddWithValue("@PageIndex", bookmark.PageIndex);
        command.Parameters.AddWithValue("@CfiLocation", (object?)bookmark.CfiLocation ?? DBNull.Value);
        command.Parameters.AddWithValue("@Title", bookmark.Title);
        command.Parameters.AddWithValue("@CreatedAt", bookmark.CreatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteBookmarkAsync(string bookmarkId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = "DELETE FROM Bookmarks WHERE Id = @Id";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", bookmarkId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<Highlight>> GetHighlightsAsync(string bookId)
    {
        var result = new List<Highlight>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = "SELECT * FROM Highlights WHERE BookId = @BookId ORDER BY CreatedAt ASC";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@BookId", bookId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new Highlight
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                BookId = reader.GetString(reader.GetOrdinal("BookId")),
                SelectedText = reader.GetString(reader.GetOrdinal("SelectedText")),
                Color = reader.GetString(reader.GetOrdinal("Color")),
                Note = reader.IsDBNull(reader.GetOrdinal("Note")) ? null : reader.GetString(reader.GetOrdinal("Note")),
                Location = reader.GetString(reader.GetOrdinal("Location")),
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
            });
        }
        return result;
    }

    public async Task AddHighlightAsync(Highlight highlight)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = @"
            INSERT OR REPLACE INTO Highlights (Id, BookId, SelectedText, Color, Note, Location, CreatedAt)
            VALUES (@Id, @BookId, @SelectedText, @Color, @Note, @Location, @CreatedAt);
        ";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", highlight.Id);
        command.Parameters.AddWithValue("@BookId", highlight.BookId);
        command.Parameters.AddWithValue("@SelectedText", highlight.SelectedText);
        command.Parameters.AddWithValue("@Color", highlight.Color);
        command.Parameters.AddWithValue("@Note", (object?)highlight.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("@Location", highlight.Location);
        command.Parameters.AddWithValue("@CreatedAt", highlight.CreatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteHighlightAsync(string highlightId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = "DELETE FROM Highlights WHERE Id = @Id";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", highlightId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<Collection>> GetCollectionsAsync()
    {
        var result = new List<Collection>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = "SELECT * FROM Collections ORDER BY Name ASC";
        using var command = new SqliteCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var coll = new Collection
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
            };
            result.Add(coll);
        }

        foreach (var coll in result)
        {
            string bookSql = "SELECT BookId FROM CollectionBooks WHERE CollectionId = @CollId";
            using var bookCmd = new SqliteCommand(bookSql, connection);
            bookCmd.Parameters.AddWithValue("@CollId", coll.Id);
            using var bookReader = await bookCmd.ExecuteReaderAsync();
            while (await bookReader.ReadAsync())
            {
                coll.BookIds.Add(bookReader.GetString(0));
            }
        }

        return result;
    }

    public async Task CreateCollectionAsync(Collection collection)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = @"
            INSERT OR REPLACE INTO Collections (Id, Name, Description, CreatedAt)
            VALUES (@Id, @Name, @Description, @CreatedAt);
        ";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", collection.Id);
        command.Parameters.AddWithValue("@Name", collection.Name);
        command.Parameters.AddWithValue("@Description", (object?)collection.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@CreatedAt", collection.CreatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task AddBookToCollectionAsync(string collectionId, string bookId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = "INSERT OR IGNORE INTO CollectionBooks (CollectionId, BookId) VALUES (@CollId, @BookId)";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@CollId", collectionId);
        command.Parameters.AddWithValue("@BookId", bookId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task RemoveBookFromCollectionAsync(string collectionId, string bookId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = "DELETE FROM CollectionBooks WHERE CollectionId = @CollId AND BookId = @BookId";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@CollId", collectionId);
        command.Parameters.AddWithValue("@BookId", bookId);
        await command.ExecuteNonQueryAsync();
    }

    private static Book ReadBook(SqliteDataReader reader)
    {
        return new Book
        {
            Id = reader.GetString(reader.GetOrdinal("Id")),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            Author = reader.GetString(reader.GetOrdinal("Author")),
            FilePath = reader.GetString(reader.GetOrdinal("FilePath")),
            FileHash = reader.GetString(reader.GetOrdinal("FileHash")),
            Format = reader.GetString(reader.GetOrdinal("Format")),
            CoverImagePath = reader.IsDBNull(reader.GetOrdinal("CoverImagePath")) ? null : reader.GetString(reader.GetOrdinal("CoverImagePath")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            TotalPages = reader.GetInt32(reader.GetOrdinal("TotalPages")),
            CurrentPage = reader.GetInt32(reader.GetOrdinal("CurrentPage")),
            ProgressPercent = reader.GetDouble(reader.GetOrdinal("ProgressPercent")),
            LastCfiLocation = reader.IsDBNull(reader.GetOrdinal("LastCfiLocation")) ? null : reader.GetString(reader.GetOrdinal("LastCfiLocation")),
            DateAdded = DateTime.Parse(reader.GetString(reader.GetOrdinal("DateAdded"))),
            LastOpened = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastOpened")))
        };
    }
}
