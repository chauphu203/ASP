using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104.Data;
using NguyenChauPhu_2121110104.Models;

namespace NguyenChauPhu_2121110104.Controllers
{
    [Route("api/attendance")]
    [ApiController]
    [Authorize]
    public class AttendanceController(AppDbContext context) : ControllerBase
    {
        public record CreateSessionRequest(int CourseId, int LecturerId, DateOnly SessionDate, TimeOnly? StartTime);
        public record ScanRequest(string Token, int StudentId);

        [HttpPost("sessions")]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<ActionResult<AttendanceSession>> CreateSession(CreateSessionRequest request)
        {
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
            return Ok(session);
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
                ScanTime = DateTime.UtcNow
            };

            context.AttendanceRecords.Add(record);
            await context.SaveChangesAsync();
            return Ok(record);
        }
    }
}
