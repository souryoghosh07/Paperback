using System;

namespace Paperback.Core.Models;

public class Highlight
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string BookId { get; set; } = string.Empty;
    public string SelectedText { get; set; } = string.Empty;
    public string Color { get; set; } = "#FFE066"; // Apple Books pastel yellow default
    public string? Note { get; set; }
    public string Location { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
