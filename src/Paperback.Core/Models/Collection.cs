using System;
using System.Collections.Generic;

namespace Paperback.Core.Models;

public class Collection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<string> BookIds { get; set; } = new();
}
