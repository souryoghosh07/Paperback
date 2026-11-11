using System;

namespace Paperback.Core.Models;

public class Book
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = "Unknown Author";
    public string FilePath { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string Format { get; set; } = "EPUB"; // "EPUB" or "PDF"
    public string? CoverImagePath { get; set; }
    public string? Description { get; set; }
    public int TotalPages { get; set; } = 1;
    public int CurrentPage { get; set; } = 1;
    public double ProgressPercent { get; set; } = 0.0;
    public string? LastCfiLocation { get; set; }
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    public DateTime LastOpened { get; set; } = DateTime.UtcNow;

    public bool IsFinished => ProgressPercent >= 99.0;

    public string ProgressDisplayText => TotalPages > 0 
        ? $"{Math.Round(ProgressPercent)}% ({CurrentPage}/{TotalPages})"
        : $"{Math.Round(ProgressPercent)}%";
}
