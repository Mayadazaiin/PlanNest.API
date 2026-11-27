using System.ComponentModel.DataAnnotations;

namespace PlanNest.API.DTOs;

public class CreateProjectDto
{
    [Required, MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(20)]
    public string ColorHex { get; set; } = "#4F46E5";
}

public class UpdateProjectDto
{
    [Required, MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(20)]
    public string ColorHex { get; set; } = "#4F46E5";
}

public class ProjectDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ColorHex { get; set; } = "#4F46E5";
    public DateTime CreatedAt { get; set; }
    public int TaskCount { get; set; }
    public int CompletedTaskCount { get; set; }
}

public class CreateTaskDto
{
    [Required, MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public int ProjectId { get; set; }

    public string Status { get; set; } = "Todo";       // Todo | InProgress | Done
    public string Priority { get; set; } = "Medium";   // Low | Medium | High
    public DateTime? DueDate { get; set; }
}

public class UpdateTaskDto
{
    [Required, MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public string Status { get; set; } = "Todo";
    public string Priority { get; set; } = "Medium";
    public DateTime? DueDate { get; set; }
}

public class TaskItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Todo";
    public string Priority { get; set; } = "Medium";
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ProjectId { get; set; }
}