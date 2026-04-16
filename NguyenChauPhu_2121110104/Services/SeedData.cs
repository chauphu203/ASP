using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104.Models;

namespace NguyenChauPhu_2121110104.Services
{
    public static class SeedData
    {
        public static async Task InitializeAsync(Data.AppDbContext db)
        {
            if (!await db.Roles.AnyAsync())
            {
                db.Roles.AddRange(
                    new Role { RoleName = "Admin", Description = "Quan tri he thong", Priority = 1 },
                    new Role { RoleName = "Lecturer", Description = "Giang vien", Priority = 2 },
                    new Role { RoleName = "Student", Description = "Sinh vien", Priority = 3 });
                await db.SaveChangesAsync();
            }

            if (!await db.Users.AnyAsync(u => u.Username == "admin"))
            {
                var admin = new User
                {
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    FullName = "System Admin",
                    Email = "admin@local.dev",
                    IsActive = true
                };
                db.Users.Add(admin);
                await db.SaveChangesAsync();

                var adminRoleId = await db.Roles.Where(r => r.RoleName == "Admin").Select(r => r.RoleId).FirstAsync();
                db.UserRoles.Add(new UserRole { UserId = admin.UserId, RoleId = adminRoleId, AssignedAt = DateTime.UtcNow });
                await db.SaveChangesAsync();
            }
        }
    }
}
