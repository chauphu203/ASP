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

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<Course>> CreateCourse(Course course)
        {
            context.Courses.Add(course);
            await context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetCourses), new { id = course.CourseId }, course);
        }
    }
}