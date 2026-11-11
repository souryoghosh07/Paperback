using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Paperback.Core.Interfaces;
using Paperback.Core.Models;
using VersOne.Epub;

namespace Paperback.Services.Epub;

public class VersOneEpubService : IEpubService
{
    public async Task<EpubMetadataResult> ExtractMetadataAsync(string epubFilePath)
    {
        if (!File.Exists(epubFilePath))
        {
            throw new FileNotFoundException("EPUB file not found", epubFilePath);
        }

        var result = new EpubMetadataResult();

        try
        {
            EpubBook epubBook = await EpubReader.ReadBookAsync(epubFilePath);

            result.Title = !string.IsNullOrWhiteSpace(epubBook.Title) 
                ? epubBook.Title 
                : Path.GetFileNameWithoutExtension(epubFilePath);

            result.Author = !string.IsNullOrWhiteSpace(epubBook.Author) 
                ? epubBook.Author 
                : "Unknown Author";

            result.Description = epubBook.Description;

            if (epubBook.CoverImage != null && epubBook.CoverImage.Length > 0)
            {
                result.CoverImageBytes = epubBook.CoverImage;
                result.CoverImageExtension = ".jpg";
            }

            if (epubBook.Navigation != null)
            {
                result.TableOfContents = MapNavigation(epubBook.Navigation, 0);
            }

            // Estimate page count: roughly 250 words per page or count reading order items
            int readingOrderCount = epubBook.ReadingOrder?.Count ?? 1;
            result.EstimatedPageCount = Math.Max(readingOrderCount * 4, 10);
        }
        catch (Exception ex)
        {
            // Fallback for corrupted or non-standard EPUBs
            result.Title = Path.GetFileNameWithoutExtension(epubFilePath);
            result.Author = "Unknown Author";
            result.Description = $"Metadata extraction note: {ex.Message}";
        }

        return result;
    }

    public async Task<string> EnsureCoverExtractedAsync(string bookId, string epubFilePath, string coversDirectory)
    {
        Directory.CreateDirectory(coversDirectory);
        string coverPath = Path.Combine(coversDirectory, $"{bookId}.jpg");

        if (File.Exists(coverPath))
        {
            return coverPath;
        }

        try
        {
            EpubBook epubBook = await EpubReader.ReadBookAsync(epubFilePath);
            if (epubBook.CoverImage != null && epubBook.CoverImage.Length > 0)
            {
                await File.WriteAllBytesAsync(coverPath, epubBook.CoverImage);
                return coverPath;
            }
        }
        catch
        {
            // Ignore error and generate fallback
        }

        // Return empty string if no embedded cover (UI will render clothbound cover)
        return string.Empty;
    }

    public async Task<List<ChapterItem>> GetTableOfContentsAsync(string epubFilePath)
    {
        try
        {
            EpubBook epubBook = await EpubReader.ReadBookAsync(epubFilePath);
            if (epubBook.Navigation != null)
            {
                return MapNavigation(epubBook.Navigation, 0);
            }
        }
        catch
        {
            // Fallback
        }
        return new List<ChapterItem>();
    }

    private static List<ChapterItem> MapNavigation(List<EpubNavigationItem> navigationItems, int level)
    {
        var result = new List<ChapterItem>();
        if (navigationItems == null) return result;

        int pageEstimate = 1;
        foreach (var item in navigationItems)
        {
            var chapter = new ChapterItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = item.Title ?? "Untitled Chapter",
                ContentSrc = item.Link?.ContentFilePath ?? item.Link?.ContentFileUrl ?? string.Empty,
                Level = level,
                PageNumber = pageEstimate
            };

            if (item.NestedItems != null && item.NestedItems.Count > 0)
            {
                chapter.Children = MapNavigation(item.NestedItems, level + 1);
            }

            result.Add(chapter);
            pageEstimate += 5;
        }

        return result;
    }
}
