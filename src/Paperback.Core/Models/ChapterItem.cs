using System.Collections.Generic;

namespace Paperback.Core.Models;

public class ChapterItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ContentSrc { get; set; } = string.Empty;
    public int Level { get; set; } = 0;
    public int PageNumber { get; set; } = 1;
    public List<ChapterItem> Children { get; set; } = new();
}
