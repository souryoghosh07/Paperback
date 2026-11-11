using System;

namespace Paperback.Core.Models;

public class Bookmark
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string BookId { get; set; } = string.Empty;
    public int PageIndex { get; set; } = 1;
    public string? CfiLocation { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
