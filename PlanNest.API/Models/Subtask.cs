using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanNest.API.Models;

public class Subtask
{
    public int Id { get; set; }

    public int TaskItemId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public int SortOrder { get; set; }

    [ForeignKey(nameof(TaskItemId))]
    public TaskItem TaskItem { get; set; } = null!;
}