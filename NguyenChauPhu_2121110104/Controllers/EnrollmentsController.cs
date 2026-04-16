using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104.Data;
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
                    Course = new { e.Course.CourseId, e.Course.CourseCode, e.Course.CourseName }
                })
                .ToListAsync();

            return Ok(rows);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<Enrollment>> CreateEnrollment(Enrollment enrollment)
        {
            context.Enrollments.Add(enrollment);
            await context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetEnrollments), new { id = enrollment.EnrollmentId }, enrollment);
        }
    }
}
