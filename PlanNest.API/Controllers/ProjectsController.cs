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
public class ProjectsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProjectsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetProjects()
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var projects = await _context.Projects
            .Where(p => p.UserId == userId.Value)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                id = p.Id,
                title = p.Title,
                description = p.Description,
                colorHex = p.ColorHex,
                createdAt = p.CreatedAt,
                taskCount = p.Tasks.Count,
                completedTaskCount = p.Tasks.Count(t => t.Status == "Done")
            })
            .ToListAsync();

        return Ok(projects);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProjectById(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var project = await _context.Projects
            .Where(p => p.Id == id && p.UserId == userId.Value)
            .Select(p => new
            {
                id = p.Id,
                title = p.Title,
                description = p.Description,
                colorHex = p.ColorHex,
                createdAt = p.CreatedAt,
                taskCount = p.Tasks.Count,
                completedTaskCount = p.Tasks.Count(t => t.Status == "Done")
            })
            .FirstOrDefaultAsync();

        if (project is null)
            return NotFound(new { message = "Project not found." });

        return Ok(project);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { message = "Project title is required." });

        var project = new Project
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ColorHex = string.IsNullOrWhiteSpace(request.ColorHex) ? "#059669" : request.ColorHex.Trim(),
            CreatedAt = DateTime.UtcNow,
            UserId = userId.Value
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetProjectById), new { id = project.Id }, new
        {
            id = project.Id,
            title = project.Title,
            description = project.Description,
            colorHex = project.ColorHex,
            createdAt = project.CreatedAt,
            taskCount = 0,
            completedTaskCount = 0
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateProject(int id, [FromBody] UpdateProjectRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { message = "Project title is required." });

        var project = await _context.Projects
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId.Value);

        if (project is null)
            return NotFound(new { message = "Project not found." });

        project.Title = request.Title.Trim();
        project.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        project.ColorHex = string.IsNullOrWhiteSpace(request.ColorHex) ? project.ColorHex : request.ColorHex.Trim();

        await _context.SaveChangesAsync();

        return Ok(new
        {
            id = project.Id,
            title = project.Title,
            description = project.Description,
            colorHex = project.ColorHex,
            createdAt = project.CreatedAt,
            taskCount = project.Tasks.Count,
            completedTaskCount = project.Tasks.Count(t => t.Status == "Done")
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteProject(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var project = await _context.Projects
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId.Value);

        if (project is null)
            return NotFound(new { message = "Project not found." });

        if (project.Tasks.Count > 0)
            _context.Tasks.RemoveRange(project.Tasks);

        _context.Projects.Remove(project);
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

public record CreateProjectRequest(
    string Title,
    string? Description,
    string ColorHex
);

public record UpdateProjectRequest(
    string Title,
    string? Description,
    string ColorHex
);