using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104.Data;
using NguyenChauPhu_2121110104.Dtos;
using NguyenChauPhu_2121110104.Models;

namespace NguyenChauPhu_2121110104.Controllers
{
    [Route("api/enrollments")]
    [ApiController]
    [Authorize]
    public class EnrollmentsController(AppDbContext context) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetEnrollments()
        {
            var rows = await context.Enrollments
                .Include(e => e.Course)
                .Include(e => e.Student)
                .Select(e => new
                {
                    e.EnrollmentId,
                    e.Semester,
                    e.Status,
                    Student = new { e.Student.UserId, e.Student.FullName, e.Student.StudentCode },
                    Course = new { e.Course.CourseId, e.Course.CourseCode, e.Course.CourseName, e.Course.Credits, e.Course.Department }
                })
                .ToListAsync();

            return Ok(rows);
        }

        /// <summary>
        /// Môn đã có lịch lớp (ClassSchedule) — GV/Admin đã mở lớp; sinh viên chỉ đăng ký các môn này.
        /// </summary>
        [HttpGet("open-courses-for-student", Order = -1)]
        public async Task<ActionResult<IEnumerable<object>>> GetOpenCoursesForStudent()
        {
            var courseIds = await context.ClassSchedules.Select(x => x.CourseId).Distinct().ToListAsync();
            if (courseIds.Count == 0)
            {
                return Ok(Array.Empty<object>());
            }

            var list = await context.Courses
                .Where(c => courseIds.Contains(c.CourseId))
                .OrderBy(c => c.CourseCode)
                .Select(c => new { c.CourseId, c.CourseCode, c.CourseName, c.Credits, c.Department })
                .ToListAsync();
            return Ok(list);
        }

        [HttpGet("{id:int}", Order = 0)]
        public async Task<ActionResult<object>> GetEnrollmentById(int id)
        {
            var row = await context.Enrollments
                .Include(e => e.Course)
                .Include(e => e.Student)
                .Where(e => e.EnrollmentId == id)
                .Select(e => new
                {
                    e.EnrollmentId,
                    e.StudentId,
                    e.CourseId,
                    e.Semester,
                    e.Status,
                    Student = new { e.Student.UserId, e.Student.FullName, e.Student.StudentCode },
                    Course = new { e.Course.CourseId, e.Course.CourseCode, e.Course.CourseName, e.Course.Credits, e.Course.Department }
                })
                .FirstOrDefaultAsync();

            if (row is null) return NotFound();
            return Ok(row);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<ActionResult<Enrollment>> CreateEnrollment(UpsertEnrollmentRequest request)
        {
            var studentExists = await context.Users
                .AnyAsync(u => u.UserId == request.StudentId && u.UserRoles.Any(ur => ur.Role.RoleName == "Student"));
            if (!studentExists)
            {
                return BadRequest("StudentId không tồn tại hoặc không phải tài khoản sinh viên.");
            }

            var courseExists = await context.Courses.AnyAsync(c => c.CourseId == request.CourseId);
            if (!courseExists)
            {
                return BadRequest("CourseId không tồn tại.");
            }

            var existed = await context.Enrollments.AnyAsync(x =>
                x.StudentId == request.StudentId &&
                x.CourseId == request.CourseId &&
                x.Semester == request.Semester);
            if (existed)
            {
                return Conflict("Sinh viên đã đăng ký môn này trong học kỳ đã chọn.");
            }

            var enrollment = new Enrollment
            {
                StudentId = request.StudentId,
                CourseId = request.CourseId,
                Semester = request.Semester,
                Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status,
                EnrollmentDate = DateTime.UtcNow
            };

            context.Enrollments.Add(enrollment);
            await context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetEnrollments), new { id = enrollment.EnrollmentId }, enrollment);
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> UpdateEnrollment(int id, UpsertEnrollmentRequest request)
        {
            var enrollment = await context.Enrollments.FindAsync(id);
            if (enrollment is null) return NotFound();

            var studentExists = await context.Users
                .AnyAsync(u => u.UserId == request.StudentId && u.UserRoles.Any(ur => ur.Role.RoleName == "Student"));
            if (!studentExists)
            {
                return BadRequest("StudentId không tồn tại hoặc không phải tài khoản sinh viên.");
            }

            var courseExists = await context.Courses.AnyAsync(c => c.CourseId == request.CourseId);
            if (!courseExists)
            {
                return BadRequest("CourseId không tồn tại.");
            }

            enrollment.StudentId = request.StudentId;
            enrollment.CourseId = request.CourseId;
            enrollment.Semester = request.Semester;
            enrollment.Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status;
            await context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteEnrollment(int id)
        {
            var enrollment = await context.Enrollments.FindAsync(id);
            if (enrollment is null) return NotFound();

            context.Enrollments.Remove(enrollment);
            await context.SaveChangesAsync();
            return NoContent();
        }
    }
}
