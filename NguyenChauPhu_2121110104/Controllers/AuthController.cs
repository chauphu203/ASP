using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104.Data;
using NguyenChauPhu_2121110104.Dtos;
using NguyenChauPhu_2121110104.Services;
using System.Security.Claims;

namespace NguyenChauPhu_2121110104.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController(AppDbContext context, JwtTokenService tokenService) : ControllerBase
    {
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult> Register(RegisterRequest request)
        {
            if (await context.Users.AnyAsync(x => x.Username == request.Username || x.Email == request.Email))
            {
                return Conflict("Username or Email already exists.");
            }

            var studentRoleId = await context.Roles
                .Where(r => r.RoleName == "Student")
                .Select(r => r.RoleId)
                .FirstOrDefaultAsync();
            if (studentRoleId == 0)
            {
                return BadRequest("Student role was not found.");
            }

            var user = new Models.User
            {
                Username = request.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FullName = request.FullName,
                Email = request.Email,
                StudentCode = request.StudentCode,
                IsActive = true
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            context.UserRoles.Add(new Models.UserRole
            {
                UserId = user.UserId,
                RoleId = studentRoleId,
                AssignedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            return Ok(new { user.UserId, user.Username, Role = "Student" });
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
        {
            var user = await context.Users.FirstOrDefaultAsync(x => x.Username == request.Username);
            if (user is null || !user.IsActive)
            {
                return Unauthorized("Invalid username or account disabled.");
            }

            var ok = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!ok)
            {
                return Unauthorized("Invalid username or password.");
            }

            user.LastLogin = DateTime.UtcNow;
            await context.SaveChangesAsync();

            var roles = await context.UserRoles
                .Where(ur => ur.UserId == user.UserId)
                .Select(ur => ur.Role.RoleName)
                .ToListAsync();

            var token = tokenService.GenerateToken(user, roles);
            return Ok(new LoginResponse(token, user.Username, roles));
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<MeResponse>> Me([FromServices] PermissionService permissionService)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await context.Users.FindAsync(userId);
            if (user is null)
            {
                return NotFound();
            }

            var roles = await context.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role.RoleName)
                .ToListAsync();
            var permissions = await permissionService.GetPermissionsForUserAsync(userId);

            return Ok(new MeResponse(user.UserId, user.Username, user.FullName, roles, permissions));
        }
    }
}
