using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PlanNest.API.Data;
using PlanNest.API.Models;

namespace PlanNest.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        return Ok(BuildAuthResponse(user));
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterRequestDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var exists = await _context.Users.AnyAsync(u => u.Email.ToLower() == email);
        if (exists)
        {
            return BadRequest(new { message = "An account with this email already exists." });
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = HashPassword(request.Password)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(BuildAuthResponse(user));
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<ActionResult<UserProfileDetailsDto>> GetProfile()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null) return NotFound();

        // Replace these queries with your actual DbSet names/ownership filters if needed
        var projectsCount = await _context.Projects.CountAsync();
        var tasks = await _context.Tasks.ToListAsync();
        var totalTasks = tasks.Count;
        var completedTasks = tasks.Count(t =>
            string.Equals(t.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Status, "Done", StringComparison.OrdinalIgnoreCase));
        var inProgressTasks = tasks.Count(t =>
            string.Equals(t.Status, "In Progress", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Status, "InProgress", StringComparison.OrdinalIgnoreCase));

        var now = DateTime.UtcNow;
        var overdueTasks = tasks.Count(t =>
            t.DueDate.HasValue &&
            t.DueDate.Value < now &&
            !string.Equals(t.Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(t.Status, "Done", StringComparison.OrdinalIgnoreCase));

        var subtasks = await _context.Subtasks.ToListAsync();
        var totalSubtasks = subtasks.Count;
        var completedSubtasks = subtasks.Count(s => s.IsCompleted);

        var completionRate = totalTasks > 0
            ? Math.Round((double)completedTasks / totalTasks * 100, 1)
            : 0;

        return Ok(new UserProfileDetailsDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            ProjectsCount = projectsCount,
            TotalTasks = totalTasks,
            CompletedTasks = completedTasks,
            InProgressTasks = inProgressTasks,
            OverdueTasks = overdueTasks,
            CompletedSubtasks = completedSubtasks,
            TotalSubtasks = totalSubtasks,
            CompletionRate = completionRate
        });
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<AuthResponseDto>> UpdateProfile([FromBody] UpdateProfileRequestDto request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new { message = "Display name is required." });
        }

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null) return NotFound();

        user.FullName = request.FullName.Trim();
        await _context.SaveChangesAsync();

        return Ok(BuildAuthResponse(user));
    }

    [Authorize]
    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { message = "Current password and new password are required." });
        }

        if (request.NewPassword.Length < 6)
        {
            return BadRequest(new { message = "New password must be at least 6 characters." });
        }

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null) return NotFound();

        if (!VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            return BadRequest(new { message = "Current password is incorrect." });
        }

        user.PasswordHash = HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Password updated successfully." });
    }

    private int? GetCurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(idClaim, out var id) ? id : null;
    }

    private AuthResponseDto BuildAuthResponse(User user)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("fullName", user.FullName)
        };

        var tokenDescriptor = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);

        return new AuthResponseDto
        {
            Token = tokenString,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email
        };
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            100_000,
            HashAlgorithmName.SHA256,
            32
        );
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            100_000,
            HashAlgorithmName.SHA256,
            32
        );
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}

public record LoginRequestDto(string Email, string Password);
public record RegisterRequestDto(string FullName, string Email, string Password);
public record UpdateProfileRequestDto(string FullName);
public record ChangePasswordRequestDto(string CurrentPassword, string NewPassword);

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class UserProfileDetailsDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int ProjectsCount { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int CompletedSubtasks { get; set; }
    public int TotalSubtasks { get; set; }
    public double CompletionRate { get; set; }
}