using System;

namespace devopsnet.Dto;

public class PipelineResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CloneUrl { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string Technology { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}