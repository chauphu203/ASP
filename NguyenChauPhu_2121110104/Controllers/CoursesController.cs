using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104.Data;
using NguyenChauPhu_2121110104.Models;

namespace NguyenChauPhu_2121110104.Controllers
{
    [Route("api/courses")]
    [ApiController]
    [Authorize]
    public class CoursesController(AppDbContext context) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Course>>> GetCourses()
        {
            return Ok(await context.Courses.ToListAsync());
        }

        /// <summary>
        /// Alias cho client cũ: môn đã có lịch lớp (ClassSchedule). Cùng dữ liệu với GET /api/enrollments/open-courses-for-student.
        /// </summary>
        [HttpGet("open-for-student", Order = -1)]
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
        public async Task<ActionResult<Course>> GetCourseById(int id)
        {
            var course = await context.Courses.FindAsync(id);
            if (course is null) return NotFound();
            return Ok(course);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<Course>> CreateCourse(Course course)
        {
            context.Courses.Add(course);
            await context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetCourses), new { id = course.CourseId }, course);
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> UpdateCourse(int id, Course request)
        {
            var course = await context.Courses.FindAsync(id);
            if (course is null) return NotFound();

            course.CourseCode = request.CourseCode;
            course.CourseName = request.CourseName;
            course.Credits = request.Credits;
            course.Department = request.Department;
            await context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteCourse(int id)
        {
            var course = await context.Courses.FindAsync(id);
            if (course is null) return NotFound();

            context.Courses.Remove(course);
            await context.SaveChangesAsync();
            return NoContent();
        }
    }
}