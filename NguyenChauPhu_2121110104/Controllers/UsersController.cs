using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104.Data;
using NguyenChauPhu_2121110104.Dtos;
using NguyenChauPhu_2121110104.Models;
using System.Security.Claims;

namespace NguyenChauPhu_2121110104.Controllers
{
    [Route("api/users")]
    [ApiController]
    [Authorize]
    public class UsersController(AppDbContext context) : ControllerBase
    {
        [HttpGet]
        [Authorize(Roles = "Admin,Lecturer,Student")]
        public async Task<ActionResult<IEnumerable<object>>> GetUsers()
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (User.IsInRole("Student") && !User.IsInRole("Admin") && !User.IsInRole("Lecturer"))
            {
                var self = await context.Users
                    .Where(u => u.UserId == currentUserId)
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
                return Ok(self);
            }

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

        [HttpGet("students")]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<ActionResult<IEnumerable<object>>> GetStudents()
        {
            var students = await context.Users
                .Where(u => u.UserRoles.Any(ur => ur.Role.RoleName == "Student"))
                .Select(u => new
                {
                    u.UserId,
                    u.StudentCode,
                    u.FullName,
                    u.Email,
                    u.IsActive
                })
                .OrderBy(x => x.StudentCode)
                .ToListAsync();
            return Ok(students);
        }

        [HttpGet("lecturers")]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<ActionResult<IEnumerable<object>>> GetLecturers()
        {
            var lecturers = await context.Users
                .Where(u => u.UserRoles.Any(ur => ur.Role.RoleName == "Lecturer"))
                .Select(u => new
                {
                    u.UserId,
                    u.LecturerCode,
                    u.FullName,
                    u.Email
                })
                .OrderBy(x => x.LecturerCode)
                .ToListAsync();
            return Ok(lecturers);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<ActionResult> CreateUser(CreateUserRequest request)
        {
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin)
            {
                var notAllowed = request.Roles
                    .Select(r => r.Trim())
                    .Where(r => !string.Equals(r, "Student", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (notAllowed.Count > 0)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Lecturer can only create Student accounts.");
                }
            }

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

        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,Lecturer,Student")]
        public async Task<ActionResult<object>> GetUserById(int id)
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (User.IsInRole("Student") && !User.IsInRole("Admin") && !User.IsInRole("Lecturer") && id != currentUserId)
            {
                return Forbid();
            }

            var user = await context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Where(u => u.UserId == id)
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.FullName,
                    u.Email,
                    u.StudentCode,
                    u.LecturerCode,
                    u.IsActive,
                    Roles = u.UserRoles.Select(ur => ur.Role.RoleName).ToList()
                })
                .FirstOrDefaultAsync();
            if (user is null) return NotFound();
            return Ok(user);
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<ActionResult> UpdateUser(int id, CreateUserRequest request)
        {
            var user = await context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.UserId == id);
            if (user is null) return NotFound();

            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin)
            {
                var targetIsStudent = user.UserRoles.Any(ur => ur.Role.RoleName == "Student");
                if (!targetIsStudent)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Lecturer can only update Student accounts.");
                }

                var notAllowedRoles = request.Roles
                    .Select(r => r.Trim())
                    .Where(r => !string.Equals(r, "Student", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (notAllowedRoles.Count > 0)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Lecturer can only assign Student role.");
                }
            }

            var conflict = await context.Users.AnyAsync(x =>
                x.UserId != id && (x.Username == request.Username || x.Email == request.Email));
            if (conflict)
            {
                return Conflict("Username or Email already exists.");
            }

            user.Username = request.Username;
            user.FullName = request.FullName;
            user.Email = request.Email;
            user.StudentCode = request.StudentCode;
            user.LecturerCode = request.LecturerCode;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            user.IsActive = true;

            context.UserRoles.RemoveRange(user.UserRoles);
            var roleIds = await context.Roles
                .Where(r => request.Roles.Contains(r.RoleName))
                .Select(r => r.RoleId)
                .ToListAsync();
            context.UserRoles.AddRange(roleIds.Select(roleId => new UserRole
            {
                UserId = user.UserId,
                RoleId = roleId,
                AssignedAt = DateTime.UtcNow
            }));

            await context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("me")]
        [Authorize(Roles = "Student")]
        public async Task<ActionResult> UpdateMyProfile(UpdateMyProfileRequest request)
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await context.Users.FirstOrDefaultAsync(u => u.UserId == currentUserId);
            if (user is null) return NotFound();

            var conflict = await context.Users.AnyAsync(x =>
                x.UserId != currentUserId && x.Email == request.Email);
            if (conflict)
            {
                return Conflict("Email already exists.");
            }

            user.FullName = request.FullName;
            user.Email = request.Email;
            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            }

            await context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteUser(int id)
        {
            var user = await context.Users.FindAsync(id);
            if (user is null) return NotFound();

            context.Users.Remove(user);
            await context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("students/bulk")]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<ActionResult<object>> CreateStudentsBulk(CreateStudentsBulkRequest request)
        {
            if (request.Students.Count == 0)
            {
                return BadRequest("Students list is empty.");
            }

            var studentRoleId = await context.Roles
                .Where(r => r.RoleName == "Student")
                .Select(r => r.RoleId)
                .FirstOrDefaultAsync();
            if (studentRoleId == 0)
            {
                return BadRequest("Student role was not found.");
            }

            var inserted = 0;
            var skipped = 0;

            foreach (var item in request.Students)
            {
                var username = item.StudentCode.ToLowerInvariant();
                var existed = await context.Users.AnyAsync(u =>
                    u.Username == username || u.StudentCode == item.StudentCode || u.Email == item.Email);
                if (existed)
                {
                    skipped++;
                    continue;
                }

                var user = new User
                {
                    Username = username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.DefaultPassword),
                    FullName = item.FullName,
                    Email = item.Email,
                    StudentCode = item.StudentCode,
                    IsActive = true
                };
                context.Users.Add(user);
                await context.SaveChangesAsync();

                context.UserRoles.Add(new UserRole
                {
                    UserId = user.UserId,
                    RoleId = studentRoleId,
                    AssignedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
                inserted++;
            }

            return Ok(new { inserted, skipped });
        }
    }
}
