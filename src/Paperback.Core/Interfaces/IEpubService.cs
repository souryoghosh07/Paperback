using System.Collections.Generic;
using System.Threading.Tasks;
using Paperback.Core.Models;

namespace Paperback.Core.Interfaces;

public class EpubMetadataResult
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string? Description { get; set; }
    public byte[]? CoverImageBytes { get; set; }
    public string? CoverImageExtension { get; set; }
    public List<ChapterItem> TableOfContents { get; set; } = new();
    public int EstimatedPageCount { get; set; } = 1;
}

public interface IEpubService
{
    Task<EpubMetadataResult> ExtractMetadataAsync(string epubFilePath);
    Task<string> EnsureCoverExtractedAsync(string bookId, string epubFilePath, string coversDirectory);
    Task<List<ChapterItem>> GetTableOfContentsAsync(string epubFilePath);
}
