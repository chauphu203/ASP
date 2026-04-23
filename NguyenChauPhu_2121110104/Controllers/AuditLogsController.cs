using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104.Data;

namespace NguyenChauPhu_2121110104.Controllers
{
    [Route("api/audit-logs")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AuditLogsController(AppDbContext context) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetLogs()
        {
            var logs = await context.AuditLogs
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    x.AuditLogId,
                    x.Action,
                    x.EntityName,
                    x.EntityId,
                    x.Details,
                    x.CreatedAt,
                    User = x.User != null ? x.User.Username : null
                })
                .ToListAsync();

            return Ok(logs);
        }
    }
}
