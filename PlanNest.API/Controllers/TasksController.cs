using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanNest.API.Data;
using PlanNest.API.Models;

namespace PlanNest.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly AppDbContext _context;

    public TasksController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetTasks([FromQuery] int? projectId)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            var query = _context.Tasks
                .AsNoTracking()
                .Include(t => t.Project)
                .Include(t => t.Subtasks)
                .Where(t => t.Project.UserId == userId.Value);

            if (projectId.HasValue)
                query = query.Where(t => t.ProjectId == projectId.Value);

            var tasks = await query
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TaskResponseDto(
                    t.Id,
                    t.Title,
                    t.Description,
                    t.Status,
                    t.Priority,
                    t.DueDate,
                    t.CreatedAt,
                    t.ProjectId,
                    t.Subtasks
                        .OrderBy(s => s.SortOrder)
                        .ThenBy(s => s.Id)
                        .Select(s => new SubtaskDto(s.Id, s.TaskItemId, s.Title, s.IsCompleted, s.SortOrder))
                        .ToList()
                ))
                .ToListAsync();

            return Ok(tasks);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Failed to load tasks.",
                detail = ex.InnerException?.Message ?? ex.Message
            });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTaskById(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var task = await _context.Tasks
            .AsNoTracking()
            .Include(t => t.Project)
            .Include(t => t.Subtasks)
            .Where(t => t.Id == id && t.Project.UserId == userId.Value)
            .Select(t => new TaskResponseDto(
                t.Id,
                t.Title,
                t.Description,
                t.Status,
                t.Priority,
                t.DueDate,
                t.CreatedAt,
                t.ProjectId,
                t.Subtasks
                    .OrderBy(s => s.SortOrder)
                    .ThenBy(s => s.Id)
                    .Select(s => new SubtaskDto(s.Id, s.TaskItemId, s.Title, s.IsCompleted, s.SortOrder))
                    .ToList()
            ))
            .FirstOrDefaultAsync();

        if (task is null)
            return NotFound(new { message = "Task not found." });

        return Ok(task);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { message = "Task title is required." });

        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.UserId == userId.Value);

        if (project is null)
            return NotFound(new { message = "Project not found." });

        var task = new TaskItem
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ProjectId = project.Id,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Todo" : request.Status.Trim(),
            Priority = string.IsNullOrWhiteSpace(request.Priority) ? "Medium" : request.Priority.Trim(),
            DueDate = request.DueDate,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        var dto = new TaskResponseDto(
            task.Id,
            task.Title,
            task.Description,
            task.Status,
            task.Priority,
            task.DueDate,
            task.CreatedAt,
            task.ProjectId,
            new List<SubtaskDto>()
        );

        return CreatedAtAction(nameof(GetTaskById), new { id = task.Id }, dto);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTask(int id, [FromBody] UpdateTaskRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { message = "Task title is required." });

        var task = await _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.Subtasks)
            .FirstOrDefaultAsync(t => t.Id == id && t.Project.UserId == userId.Value);

        if (task is null)
            return NotFound(new { message = "Task not found." });

        task.Title = request.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        task.Status = string.IsNullOrWhiteSpace(request.Status) ? task.Status : request.Status.Trim();
        task.Priority = string.IsNullOrWhiteSpace(request.Priority) ? task.Priority : request.Priority.Trim();
        task.DueDate = request.DueDate;

        await _context.SaveChangesAsync();

        var dto = new TaskResponseDto(
            task.Id,
            task.Title,
            task.Description,
            task.Status,
            task.Priority,
            task.DueDate,
            task.CreatedAt,
            task.ProjectId,
            task.Subtasks
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.Id)
                .Select(s => new SubtaskDto(s.Id, s.TaskItemId, s.Title, s.IsCompleted, s.SortOrder))
                .ToList()
        );

        return Ok(dto);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var task = await _context.Tasks
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == id && t.Project.UserId == userId.Value);

        if (task is null)
            return NotFound(new { message = "Task not found." });

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{taskId:int}/subtasks")]
    public async Task<IActionResult> CreateSubtask(int taskId, [FromBody] CreateSubtaskRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { message = "Subtask title is required." });

        var task = await _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.Subtasks)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.Project.UserId == userId.Value);

        if (task is null)
            return NotFound(new { message = "Task not found." });

        var nextOrder = task.Subtasks.Count == 0 ? 0 : task.Subtasks.Max(s => s.SortOrder) + 1;

        var subtask = new Subtask
        {
            TaskItemId = task.Id,
            Title = request.Title.Trim(),
            IsCompleted = false,
            SortOrder = request.SortOrder ?? nextOrder
        };

        _context.Subtasks.Add(subtask);
        await _context.SaveChangesAsync();

        return Ok(new SubtaskDto(subtask.Id, subtask.TaskItemId, subtask.Title, subtask.IsCompleted, subtask.SortOrder));
    }

    [HttpPut("{taskId:int}/subtasks/{subtaskId:int}")]
    public async Task<IActionResult> UpdateSubtask(int taskId, int subtaskId, [FromBody] UpdateSubtaskRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var subtask = await _context.Subtasks
            .Include(s => s.TaskItem)
                .ThenInclude(t => t.Project)
            .FirstOrDefaultAsync(s => s.Id == subtaskId && s.TaskItemId == taskId && s.TaskItem.Project.UserId == userId.Value);

        if (subtask is null)
            return NotFound(new { message = "Subtask not found." });

        if (!string.IsNullOrWhiteSpace(request.Title))
            subtask.Title = request.Title.Trim();

        subtask.IsCompleted = request.IsCompleted;
        if (request.SortOrder.HasValue)
            subtask.SortOrder = request.SortOrder.Value;

        await _context.SaveChangesAsync();

        return Ok(new SubtaskDto(subtask.Id, subtask.TaskItemId, subtask.Title, subtask.IsCompleted, subtask.SortOrder));
    }

    [HttpDelete("{taskId:int}/subtasks/{subtaskId:int}")]
    public async Task<IActionResult> DeleteSubtask(int taskId, int subtaskId)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var subtask = await _context.Subtasks
            .Include(s => s.TaskItem)
                .ThenInclude(t => t.Project)
            .FirstOrDefaultAsync(s => s.Id == subtaskId && s.TaskItemId == taskId && s.TaskItem.Project.UserId == userId.Value);

        if (subtask is null)
            return NotFound(new { message = "Subtask not found." });

        _context.Subtasks.Remove(subtask);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private int? GetCurrentUserId()
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("userId");

        return int.TryParse(rawId, out var id) ? id : null;
    }
}

public record CreateTaskRequest(
    string Title,
    string? Description,
    int ProjectId,
    string Status,
    string Priority,
    DateTime? DueDate
);

public record UpdateTaskRequest(
    string Title,
    string? Description,
    string Status,
    string Priority,
    DateTime? DueDate
);

public record CreateSubtaskRequest(string Title, int? SortOrder);
public record UpdateSubtaskRequest(string? Title, bool IsCompleted, int? SortOrder);
public record SubtaskDto(int Id, int TaskItemId, string Title, bool IsCompleted, int SortOrder);

public record TaskResponseDto(
    int Id,
    string Title,
    string? Description,
    string Status,
    string Priority,
    DateTime? DueDate,
    DateTime CreatedAt,
    int ProjectId,
    List<SubtaskDto> Subtasks
);