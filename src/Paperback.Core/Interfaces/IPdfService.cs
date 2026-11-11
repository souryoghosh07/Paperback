using System.Collections.Generic;
using System.Threading.Tasks;
using Paperback.Core.Models;

namespace Paperback.Core.Interfaces;

public class PdfMetadataResult
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int PageCount { get; set; } = 1;
    public List<ChapterItem> TableOfContents { get; set; } = new();
}

public interface IPdfService
{
    Task<PdfMetadataResult> ExtractMetadataAsync(string pdfFilePath);
    Task<string> EnsureCoverExtractedAsync(string bookId, string pdfFilePath, string coversDirectory);
}
