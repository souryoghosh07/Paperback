using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Paperback.Core.Interfaces;
using Paperback.Core.Models;

namespace Paperback.Services.Pdf;

public class WindowsPdfService : IPdfService
{
    public Task<PdfMetadataResult> ExtractMetadataAsync(string pdfFilePath)
    {
        if (!File.Exists(pdfFilePath))
        {
            throw new FileNotFoundException("PDF file not found", pdfFilePath);
        }

        var result = new PdfMetadataResult
        {
            Title = Path.GetFileNameWithoutExtension(pdfFilePath),
            Author = "PDF Document",
            PageCount = 1
        };

        try
        {
            // Read first 64KB and last 64KB to extract standard PDF metadata and page count
            using var stream = File.OpenRead(pdfFilePath);
            int bufferSize = (int)Math.Min(stream.Length, 128 * 1024);
            byte[] buffer = new byte[bufferSize];
            
            // Read header
            int bytesRead = stream.Read(buffer, 0, bufferSize);
            string headerText = Encoding.ASCII.GetString(buffer, 0, bytesRead);

            // Read trailer if file is larger
            string trailerText = "";
            if (stream.Length > bufferSize)
            {
                stream.Seek(Math.Max(0, stream.Length - bufferSize), SeekOrigin.Begin);
                int trailerRead = stream.Read(buffer, 0, bufferSize);
                trailerText = Encoding.ASCII.GetString(buffer, 0, trailerRead);
            }

            string combined = headerText + "\n" + trailerText;

            // Extract /Title
            var titleMatch = Regex.Match(combined, @"/Title\s*\(([^)]+)\)");
            if (titleMatch.Success && !string.IsNullOrWhiteSpace(titleMatch.Groups[1].Value))
            {
                result.Title = CleanPdfString(titleMatch.Groups[1].Value);
            }

            // Extract /Author
            var authorMatch = Regex.Match(combined, @"/Author\s*\(([^)]+)\)");
            if (authorMatch.Success && !string.IsNullOrWhiteSpace(authorMatch.Groups[1].Value))
            {
                result.Author = CleanPdfString(authorMatch.Groups[1].Value);
            }

            // Extract /Count (page count)
            var countMatch = Regex.Match(combined, @"/Type\s*/Pages.*?/Count\s+(\d+)", RegexOptions.Singleline);
            if (countMatch.Success && int.TryParse(countMatch.Groups[1].Value, out int count) && count > 0)
            {
                result.PageCount = count;
            }
            else
            {
                // Fallback: estimate or count /Type /Page
                var pageMatches = Regex.Matches(combined, @"/Type\s*/Page\b");
                if (pageMatches.Count > 0)
                {
                    result.PageCount = pageMatches.Count;
                }
                else
                {
                    result.PageCount = Math.Max(1, (int)(stream.Length / (50 * 1024))); // ~50KB per page rough estimate
                }
            }
        }
        catch
        {
            // Fallback to filename-based defaults
        }

        return Task.FromResult(result);
    }

    public Task<string> EnsureCoverExtractedAsync(string bookId, string pdfFilePath, string coversDirectory)
    {
        Directory.CreateDirectory(coversDirectory);
        string coverPath = Path.Combine(coversDirectory, $"{bookId}.png");

        if (File.Exists(coverPath))
        {
            return Task.FromResult(coverPath);
        }

        // Return empty string so UI renders the rich Apple Books PDF card
        return Task.FromResult(string.Empty);
    }

    private static string CleanPdfString(string input)
    {
        // Decode common PDF octal or escaped chars
        return input.Replace("\\(", "(").Replace("\\)", ")").Replace("\\\\", "\\").Trim();
    }
}
