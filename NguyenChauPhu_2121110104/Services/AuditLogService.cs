using NguyenChauPhu_2121110104.Data;
using NguyenChauPhu_2121110104.Models;
using System.Security.Claims;

namespace NguyenChauPhu_2121110104.Services
{
    public class AuditLogService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        public async Task LogAsync(string action, string entityName, string entityId, string? details = null)
        {
            var userIdText = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? userId = int.TryParse(userIdText, out var parsed) ? parsed : null;

            context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                Details = details,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        }
    }
}
