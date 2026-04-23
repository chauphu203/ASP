using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104.Data;
using NguyenChauPhu_2121110104.Models;
using NguyenChauPhu_2121110104.Services;

namespace NguyenChauPhu_2121110104.Controllers
{
    [Route("api/attendance")]
    [ApiController]
    [Authorize]
    public class AttendanceController(AppDbContext context, AuditLogService auditLogService) : ControllerBase
    {
        public record CreateSessionRequest(int CourseId, int LecturerId, DateOnly SessionDate, TimeOnly? StartTime);
        public record ScanRequest(string Token, int StudentId, double? Latitude, double? Longitude);

        [HttpPost("sessions")]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<ActionResult<AttendanceSession>> CreateSession(CreateSessionRequest request)
        {
            var courseOk = await context.Courses.AnyAsync(c => c.CourseId == request.CourseId);
            if (!courseOk)
            {
                return BadRequest("CourseId không tồn tại. Mở Môn học và dùng đúng mã CourseId (số trong bảng).");
            }

            var lecturerOk = await context.UserRoles.AnyAsync(ur =>
                ur.UserId == request.LecturerId && ur.Role.RoleName == "Lecturer");
            if (!lecturerOk)
            {
                return BadRequest("LecturerId không hợp lệ. Dùng UserId của tài khoản có vai trò Giảng viên (cột đầu trong Người dùng).");
            }

            var session = new AttendanceSession
            {
                CourseId = request.CourseId,
                LecturerId = request.LecturerId,
                SessionDate = request.SessionDate,
                StartTime = request.StartTime,
                QRToken = Guid.NewGuid().ToString("N"),
                TokenExpiry = DateTime.UtcNow.AddMinutes(15)
            };

            context.AttendanceSessions.Add(session);
            await context.SaveChangesAsync();
            await auditLogService.LogAsync("CreateAttendanceSession", "AttendanceSession", session.SessionId.ToString(), $"CourseId={session.CourseId}");
            return Ok(session);
        }

        [HttpPost("sessions/{sessionId:int}/refresh-token")]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<ActionResult<object>> RefreshToken(int sessionId)
        {
            var session = await context.AttendanceSessions.FindAsync(sessionId);
            if (session is null) return NotFound();

            session.QRToken = Guid.NewGuid().ToString("N");
            session.TokenExpiry = DateTime.UtcNow.AddMinutes(15);
            await context.SaveChangesAsync();
            return Ok(new { session.SessionId, session.QRToken, session.TokenExpiry });
        }

        [HttpGet("sessions")]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<ActionResult<IEnumerable<object>>> GetSessions()
        {
            var rows = await context.AttendanceSessions
                .AsNoTracking()
                .Include(x => x.Course)
                .OrderByDescending(x => x.SessionId)
                .ToListAsync();

            var sessions = rows.Select(x => new
            {
                x.SessionId,
                SessionDate = x.SessionDate.ToString("yyyy-MM-dd"),
                StartTime = x.StartTime?.ToString(@"HH\:mm"),
                x.QRToken,
                x.TokenExpiry,
                Course = x.Course?.CourseName ?? ""
            }).ToList();

            return Ok(sessions);
        }

        [HttpGet("sessions/{sessionId:int}")]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<ActionResult<object>> GetSessionById(int sessionId)
        {
            var x = await context.AttendanceSessions
                .AsNoTracking()
                .Include(s => s.Course)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);
            if (x is null) return NotFound();

            return Ok(new
            {
                x.SessionId,
                x.CourseId,
                x.LecturerId,
                SessionDate = x.SessionDate.ToString("yyyy-MM-dd"),
                StartTime = x.StartTime?.ToString(@"HH\:mm"),
                x.QRToken,
                x.TokenExpiry,
                Course = x.Course?.CourseName ?? ""
            });
        }

        [HttpPut("sessions/{sessionId:int}")]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<ActionResult> UpdateSession(int sessionId, CreateSessionRequest request)
        {
            var session = await context.AttendanceSessions.FindAsync(sessionId);
            if (session is null) return NotFound();

            var currentUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (!User.IsInRole("Admin") && session.LecturerId != currentUserId)
            {
                return Forbid();
            }

            session.CourseId = request.CourseId;
            session.LecturerId = request.LecturerId;
            session.SessionDate = request.SessionDate;
            session.StartTime = request.StartTime;
            await context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("sessions/{sessionId:int}")]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<ActionResult> DeleteSession(int sessionId)
        {
            var session = await context.AttendanceSessions.FindAsync(sessionId);
            if (session is null) return NotFound();
            if (!User.IsInRole("Admin") && session.LecturerId != int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0"))
            {
                return Forbid();
            }

            context.AttendanceSessions.Remove(session);
            await context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("scan")]
        [Authorize(Roles = "Student,Admin")]
        public async Task<ActionResult<AttendanceRecord>> Scan(ScanRequest request)
        {
            var session = await context.AttendanceSessions
                .FirstOrDefaultAsync(x => x.QRToken == request.Token);
            if (session is null || session.TokenExpiry < DateTime.UtcNow)
            {
                return BadRequest("QR token invalid or expired.");
            }

            var enrolled = await context.Enrollments
                .AnyAsync(e => e.CourseId == session.CourseId && e.StudentId == request.StudentId && e.Status == "Active");
            if (!enrolled)
            {
                return BadRequest("Student is not enrolled for this course.");
            }

            var existed = await context.AttendanceRecords
                .FirstOrDefaultAsync(r => r.SessionId == session.SessionId && r.StudentId == request.StudentId);
            if (existed is not null)
            {
                return Ok(existed);
            }

            var record = new AttendanceRecord
            {
                SessionId = session.SessionId,
                StudentId = request.StudentId,
                Status = "Present",
                ScanTime = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                Latitude = request.Latitude,
                Longitude = request.Longitude
            };

            context.AttendanceRecords.Add(record);
            await context.SaveChangesAsync();
            await auditLogService.LogAsync("AttendanceScan", "AttendanceRecord", record.RecordId.ToString(), $"SessionId={record.SessionId};StudentId={record.StudentId}");
            return Ok(record);
        }
    }
}
