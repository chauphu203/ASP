using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104.Data;
using NguyenChauPhu_2121110104.Dtos;
using NguyenChauPhu_2121110104.Models;

namespace NguyenChauPhu_2121110104.Controllers
{
    [Route("api/users")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class UsersController(AppDbContext context) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetUsers()
        {
            var users = await context.Users
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.FullName,
                    u.Email,
                    u.StudentCode,
                    u.LecturerCode,
                    u.IsActive
                })
                .ToListAsync();
            return Ok(users);
        }

        [HttpPost]
        public async Task<ActionResult> CreateUser(CreateUserRequest request)
        {
            if (await context.Users.AnyAsync(x => x.Username == request.Username || x.Email == request.Email))
            {
                return Conflict("Username or Email already exists.");
            }

            var user = new User
            {
                Username = request.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FullName = request.FullName,
                Email = request.Email,
                StudentCode = request.StudentCode,
                LecturerCode = request.LecturerCode,
                IsActive = true
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var roleIds = await context.Roles
                .Where(r => request.Roles.Contains(r.RoleName))
                .Select(r => r.RoleId)
                .ToListAsync();

            foreach (var roleId in roleIds)
            {
                context.UserRoles.Add(new UserRole
                {
                    UserId = user.UserId,
                    RoleId = roleId,
                    AssignedAt = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetUsers), new { id = user.UserId }, new { user.UserId, user.Username });
        }
    }
}
