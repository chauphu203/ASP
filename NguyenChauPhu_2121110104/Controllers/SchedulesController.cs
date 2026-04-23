using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104.Data;
using NguyenChauPhu_2121110104.Dtos;
using NguyenChauPhu_2121110104.Models;
using System.Security.Claims;

namespace NguyenChauPhu_2121110104.Controllers
{
    [Route("api/schedules")]
    [ApiController]
    [Authorize]
    public class SchedulesController(AppDbContext context) : ControllerBase
    {
        /// <summary>Tạo lịch lớp mặc định để môn được coi là đã mở; sinh viên tự đăng ký qua /api/enrollments (học kỳ do SV chọn).</summary>
        [HttpPost("classes/quick-open")]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<ActionResult<ClassSchedule>> QuickOpenClass(QuickOpenClassRequest request)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var lecturerId = request.LecturerId ?? userId;
            if (User.IsInRole("Lecturer") && !User.IsInRole("Admin"))
            {
                lecturerId = userId;
            }

            var courseOk = await context.Courses.AnyAsync(c => c.CourseId == request.CourseId);
            if (!courseOk)
            {
                return BadRequest("Môn học không tồn tại.");
            }

            var lecturerOk = await context.UserRoles.AnyAsync(ur =>
                ur.UserId == lecturerId && ur.Role.RoleName == "Lecturer");
            if (!lecturerOk)
            {
                return BadRequest("Giảng viên phụ trách không hợp lệ.");
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var row = new ClassSchedule
            {
                CourseId = request.CourseId,
                LecturerId = lecturerId,
                Room = "—",
                DayOfWeek = "Chưa xếp",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(10, 0),
                StartDate = today,
                EndDate = today.AddMonths(6)
            };
            context.ClassSchedules.Add(row);
            await context.SaveChangesAsync();
            return Ok(row);
        }

        [HttpPost("classes")]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<ActionResult<ClassSchedule>> CreateClassSchedule(CreateClassScheduleRequest request)
        {
            var row = new ClassSchedule
            {
                CourseId = request.CourseId,
                LecturerId = request.LecturerId,
                Room = request.Room,
                DayOfWeek = request.DayOfWeek,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                StartDate = request.StartDate,
                EndDate = request.EndDate
            };
            context.ClassSchedules.Add(row);
            await context.SaveChangesAsync();
            return Ok(row);
        }

        [HttpPost("exams")]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<ActionResult<ExamSchedule>> CreateExamSchedule(CreateExamScheduleRequest request)
        {
            var row = new ExamSchedule
            {
                CourseId = request.CourseId,
                LecturerId = request.LecturerId,
                ExamDate = request.ExamDate,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Room = request.Room,
                ExamType = request.ExamType
            };
            context.ExamSchedules.Add(row);
            await context.SaveChangesAsync();
            return Ok(row);
        }

        [HttpGet("classes")]
        public async Task<ActionResult<IEnumerable<object>>> GetClassSchedules()
        {
            var rows = await context.ClassSchedules
                .Include(x => x.Course)
                .Include(x => x.Lecturer)
                .Select(x => new
                {
                    x.ClassScheduleId,
                    x.DayOfWeek,
                    x.StartTime,
                    x.EndTime,
                    x.StartDate,
                    x.EndDate,
                    x.Room,
                    Course = x.Course.CourseName,
                    Lecturer = x.Lecturer.FullName
                })
                .ToListAsync();
            return Ok(rows);
        }

        [HttpGet("exams")]
        public async Task<ActionResult<IEnumerable<object>>> GetExamSchedules()
        {
            var rows = await context.ExamSchedules
                .Include(x => x.Course)
                .Include(x => x.Lecturer)
                .Select(x => new
                {
                    x.ExamScheduleId,
                    x.ExamDate,
                    x.StartTime,
                    x.EndTime,
                    x.Room,
                    x.ExamType,
                    Course = x.Course.CourseName,
                    Lecturer = x.Lecturer.FullName
                })
                .ToListAsync();
            return Ok(rows);
        }

        [HttpGet("student/{studentId:int}")]
        public async Task<ActionResult<object>> GetStudentSchedules(int studentId)
        {
            var courseIds = await context.Enrollments
                .Where(e => e.StudentId == studentId && e.Status == "Active")
                .Select(e => e.CourseId)
                .ToListAsync();

            var classes = await context.ClassSchedules
                .Where(x => courseIds.Contains(x.CourseId))
                .Include(x => x.Course)
                .Select(x => new
                {
                    x.ClassScheduleId,
                    x.DayOfWeek,
                    x.StartTime,
                    x.EndTime,
                    x.Room,
                    x.StartDate,
                    x.EndDate,
                    x.Course.CourseCode,
                    x.Course.CourseName
                })
                .ToListAsync();

            var exams = await context.ExamSchedules
                .Where(x => courseIds.Contains(x.CourseId))
                .Include(x => x.Course)
                .Select(x => new
                {
                    x.ExamScheduleId,
                    x.ExamDate,
                    x.StartTime,
                    x.EndTime,
                    x.Room,
                    x.ExamType,
                    x.Course.CourseCode,
                    x.Course.CourseName
                })
                .ToListAsync();

            return Ok(new { studentId, classes, exams });
        }
    }
}
