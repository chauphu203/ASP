using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104.Data;
using NguyenChauPhu_2121110104.Models;

namespace NguyenChauPhu_2121110104.Controllers
{
    [Route("api/grades")]
    [ApiController]
    [Authorize]
    public class GradesController(AppDbContext context) : ControllerBase
    {
        [HttpPost]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<ActionResult<Grade>> UpsertGrade(Grade grade)
        {
            var enrollment = await context.Enrollments
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.EnrollmentId == grade.EnrollmentId);
            if (enrollment is null) return NotFound("Enrollment not found");

            grade.TotalScore = (grade.MidtermScore ?? 0) * 0.3 + (grade.FinalScore ?? 0) * 0.5 + (grade.AttendanceScore ?? 0) * 0.2;
            grade.GpaContribution = grade.TotalScore * enrollment.Course.Credits;

            var current = await context.Grades.FirstOrDefaultAsync(g => g.EnrollmentId == grade.EnrollmentId);
            if (current is null)
            {
                context.Grades.Add(grade);
            }
            else
            {
                current.MidtermScore = grade.MidtermScore;
                current.FinalScore = grade.FinalScore;
                current.AttendanceScore = grade.AttendanceScore;
                current.TotalScore = grade.TotalScore;
                current.GpaContribution = grade.GpaContribution;
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

            return Ok(new { studentId, totalCredits, totalPoint, gpa = Math.Round(gpa, 2) });
        }
    }
}
