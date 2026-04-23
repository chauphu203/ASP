using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104.Data;

namespace NguyenChauPhu_2121110104.Services
{
    public class PermissionService(AppDbContext context)
    {
        public async Task<List<string>> GetPermissionsForUserAsync(int userId)
        {
            return await context.UserRoles
                .Where(ur => ur.UserId == userId)
                .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.PermissionCode))
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
        }
    }
}
