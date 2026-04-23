using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104;
using NguyenChauPhu_2121110104.Data;

namespace NguyenChauPhu_2121110104.Controllers
{
    [Route("api/statistics")]
    [ApiController]
    [Authorize]
    public class StatisticsController(AppDbContext context) : ControllerBase
    {
        [HttpGet("dashboard")]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<ActionResult<object>> GetDashboard()
        {
            var totalUsers = await context.Users.CountAsync();
            var totalStudents = await context.UserRoles.CountAsync(x => x.Role.RoleName == "Student");
            var totalLecturers = await context.UserRoles.CountAsync(x => x.Role.RoleName == "Lecturer");
            var totalCourses = await context.Courses.CountAsync();
            var totalEnrollments = await context.Enrollments.CountAsync();
            var totalAttendanceSessions = await context.AttendanceSessions.CountAsync();

            var publishedGrades = await context.Grades.CountAsync(x => x.IsPublished);
            return Ok(new
            {
                totalUsers,
                totalStudents,
                totalLecturers,
                totalCourses,
                totalEnrollments,
                totalAttendanceSessions,
                publishedGrades
            });
        }

        [HttpGet("students/{studentId:int}")]
        public async Task<ActionResult<object>> GetStudentStatistics(int studentId)
        {
            var rows = await context.Enrollments
                .Where(e => e.StudentId == studentId)
                .Include(e => e.Course)
                .Include(e => e.Grade)
                .ToListAsync();

            var chartData = rows.Select(x => new
            {
                x.Course.CourseCode,
                x.Course.CourseName,
                Credits = x.Course.Credits,
                TotalScore = ScoreFormatting.Trunc2(x.Grade?.TotalScore ?? 0)
            });

            var totalCredits = rows.Where(r => r.Grade?.GpaContribution != null).Sum(r => r.Course.Credits);
            var totalPoint = rows.Sum(r => r.Grade?.GpaContribution ?? 0);
            var gpa = totalCredits == 0 ? 0 : ScoreFormatting.Trunc2(totalPoint / totalCredits);

            return Ok(new { studentId, gpa, chartData });
        }

        [HttpGet("courses")]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<ActionResult<IEnumerable<object>>> GetCourseStatistics()
        {
            var rows = await context.Courses
                .Select(c => new
                {
                    c.CourseId,
                    c.CourseCode,
                    c.CourseName,
                    EnrollmentCount = c.Enrollments.Count,
                    AverageScore = c.Enrollments.Where(e => e.Grade != null).Select(e => e.Grade!.TotalScore ?? 0).DefaultIfEmpty(0).Average()
                })
                .ToListAsync();

            return Ok(rows);
        }
    }
}
