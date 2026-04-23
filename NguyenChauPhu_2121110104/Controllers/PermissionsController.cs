using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104.Data;
using NguyenChauPhu_2121110104.Dtos;
using NguyenChauPhu_2121110104.Models;
using NguyenChauPhu_2121110104.Services;

namespace NguyenChauPhu_2121110104.Controllers
{
    [Route("api/permissions")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class PermissionsController(AppDbContext context, PermissionService permissionService) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Permission>>> GetPermissions()
        {
            return Ok(await context.Permissions.OrderBy(x => x.ModuleName).ThenBy(x => x.PermissionCode).ToListAsync());
        }

        [HttpPost]
        public async Task<ActionResult<Permission>> CreatePermission(CreatePermissionRequest request)
        {
            var permission = new Permission
            {
                PermissionCode = request.PermissionCode,
                PermissionName = request.PermissionName,
                ModuleName = request.ModuleName
            };
            context.Permissions.Add(permission);
            await context.SaveChangesAsync();
            return Ok(permission);
        }

        [HttpGet("roles")]
        public async Task<ActionResult<IEnumerable<Role>>> GetRoles()
        {
            return Ok(await context.Roles.OrderBy(x => x.Priority).ToListAsync());
        }

        [HttpGet("roles/{roleId:int}")]
        public async Task<ActionResult<object>> GetRolePermissions(int roleId)
        {
            var role = await context.Roles
                .Where(r => r.RoleId == roleId)
                .Select(r => new
                {
                    r.RoleId,
                    r.RoleName,
                    Permissions = r.RolePermissions.Select(rp => new
                    {
                        rp.PermissionId,
                        rp.Permission.PermissionCode,
                        rp.Permission.PermissionName
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            return role is null ? NotFound() : Ok(role);
        }

        [HttpPost("roles/{roleId:int}")]
        public async Task<ActionResult> AssignPermissionsToRole(int roleId, AssignPermissionsRequest request)
        {
            var role = await context.Roles.Include(r => r.RolePermissions).FirstOrDefaultAsync(r => r.RoleId == roleId);
            if (role is null) return NotFound();

            context.RolePermissions.RemoveRange(role.RolePermissions);
            var rows = request.PermissionIds.Distinct().Select(permissionId => new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId,
                GrantedAt = DateTime.UtcNow
            });
            context.RolePermissions.AddRange(rows);
            await context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("users/{userId:int}")]
        public async Task<ActionResult<object>> GetUserPermissions(int userId)
        {
            var permissions = await permissionService.GetPermissionsForUserAsync(userId);
            return Ok(new { userId, permissions });
        }

        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<object>>> GetUsersForRbac()
        {
            var users = await context.Users
                .OrderBy(x => x.Username)
                .Select(x => new
                {
                    x.UserId,
                    x.Username,
                    x.FullName,
                    x.Email
                })
                .ToListAsync();
            return Ok(users);
        }

        [HttpGet("users/{userId:int}/roles")]
        public async Task<ActionResult<object>> GetUserRoles(int userId)
        {
            var user = await context.Users
                .Where(u => u.UserId == userId)
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    Roles = u.UserRoles.Select(ur => new
                    {
                        ur.RoleId,
                        ur.Role.RoleName
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            return user is null ? NotFound() : Ok(user);
        }

        [HttpPost("users/{userId:int}/roles")]
        public async Task<ActionResult> AssignRolesToUser(int userId, AssignRolesRequest request)
        {
            var user = await context.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.UserId == userId);
            if (user is null) return NotFound();

            context.UserRoles.RemoveRange(user.UserRoles);
            var rows = request.RoleIds.Distinct().Select(roleId => new UserRole
            {
                UserId = userId,
                RoleId = roleId,
                AssignedAt = DateTime.UtcNow
            });
            context.UserRoles.AddRange(rows);
            await context.SaveChangesAsync();
            return NoContent();
        }
    }
}
