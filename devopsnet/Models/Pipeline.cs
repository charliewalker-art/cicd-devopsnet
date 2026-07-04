using System;

namespace devopsnet.Models;

public class Pipeline
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CloneUrl { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string Technology { get; set; } = "Pre-configured"; // Préparé pour le futur
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}