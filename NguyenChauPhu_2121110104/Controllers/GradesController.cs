using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104;
using NguyenChauPhu_2121110104.Data;
using NguyenChauPhu_2121110104.Models;
using NguyenChauPhu_2121110104.Services;

namespace NguyenChauPhu_2121110104.Controllers
{
    [Route("api/grades")]
    [ApiController]
    [Authorize]
    public class GradesController(AppDbContext context, AuditLogService auditLogService) : ControllerBase
    {
        public record UpsertGradeRequest(int EnrollmentId, double? MidtermScore, double? FinalScore, double? AttendanceScore);

        [HttpPost]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<ActionResult<Grade>> UpsertGrade(UpsertGradeRequest request)
        {
            var enrollment = await context.Enrollments
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.EnrollmentId == request.EnrollmentId);
            if (enrollment is null) return NotFound("Enrollment not found");

            var m = ScoreFormatting.Trunc2Nullable(request.MidtermScore);
            var f = ScoreFormatting.Trunc2Nullable(request.FinalScore);
            var a = ScoreFormatting.Trunc2Nullable(request.AttendanceScore);

            var grade = new Grade
            {
                EnrollmentId = request.EnrollmentId,
                MidtermScore = m,
                FinalScore = f,
                AttendanceScore = a
            };

            grade.TotalScore = ScoreFormatting.Trunc2((m ?? 0) * 0.3 + (f ?? 0) * 0.5 + (a ?? 0) * 0.2);
            grade.GpaContribution = ScoreFormatting.Trunc2((grade.TotalScore ?? 0) * enrollment.Course.Credits);

            var current = await context.Grades.FirstOrDefaultAsync(g => g.EnrollmentId == grade.EnrollmentId);
            if (current is null)
            {
                context.Grades.Add(grade);
                await auditLogService.LogAsync("CreateGrade", "Grades", request.EnrollmentId.ToString(), $"TotalScore={grade.TotalScore}");
            }
            else
            {
                current.MidtermScore = grade.MidtermScore;
                current.FinalScore = grade.FinalScore;
                current.AttendanceScore = grade.AttendanceScore;
                current.TotalScore = grade.TotalScore;
                current.GpaContribution = grade.GpaContribution;
                await auditLogService.LogAsync("UpdateGrade", "Grades", request.EnrollmentId.ToString(), $"TotalScore={grade.TotalScore}");
            }

            await context.SaveChangesAsync();
            return Ok(grade);
        }

        [HttpGet("gpa/{studentId:int}")]
        public async Task<ActionResult<object>> GetGpa(int studentId)
        {
            var rows = await context.Enrollments
                .Where(e => e.StudentId == studentId)
                .Include(e => e.Course)
                .Include(e => e.Grade)
                .ToListAsync();

            var totalCredits = rows.Where(r => r.Grade?.GpaContribution != null).Sum(r => r.Course.Credits);
            var totalPoint = rows.Sum(r => r.Grade?.GpaContribution ?? 0);
            var gpa = totalCredits == 0 ? 0 : totalPoint / totalCredits;

            return Ok(new { studentId, totalCredits, totalPoint, gpa = ScoreFormatting.Trunc2(gpa) });
        }

        [HttpPost("{enrollmentId:int}/publish")]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<ActionResult> PublishGrade(int enrollmentId)
        {
            var grade = await context.Grades.FirstOrDefaultAsync(g => g.EnrollmentId == enrollmentId);
            if (grade is null) return NotFound();

            grade.IsPublished = true;
            await context.SaveChangesAsync();
            await auditLogService.LogAsync("PublishGrade", "Grades", enrollmentId.ToString(), "Grade published to student");
            return NoContent();
        }

        [HttpGet("course/{courseId:int}")]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<ActionResult<IEnumerable<object>>> GetCourseGrades(int courseId)
        {
            var rows = await context.Enrollments
                .Where(e => e.CourseId == courseId)
                .Include(e => e.Student)
                .Include(e => e.Grade)
                .ToListAsync();

            var result = rows.Select(e => new
            {
                e.EnrollmentId,
                e.Student.StudentCode,
                e.Student.FullName,
                e.Semester,
                Grade = e.Grade == null ? null : new
                {
                    MidtermScore = ScoreFormatting.Trunc2Nullable(e.Grade.MidtermScore),
                    FinalScore = ScoreFormatting.Trunc2Nullable(e.Grade.FinalScore),
                    AttendanceScore = ScoreFormatting.Trunc2Nullable(e.Grade.AttendanceScore),
                    TotalScore = ScoreFormatting.Trunc2Nullable(e.Grade.TotalScore),
                    GpaContribution = ScoreFormatting.Trunc2Nullable(e.Grade.GpaContribution),
                    e.Grade.IsPublished
                }
            }).ToList();

            return Ok(result);
        }
    }
}
