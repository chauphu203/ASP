using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104.Data;
using NguyenChauPhu_2121110104;

namespace NguyenChauPhu_2121110104.Controllers
{
    [Route("api/reports")]
    [ApiController]
    [Authorize(Roles = "Admin,Lecturer")]
    public class ReportsController(AppDbContext context) : ControllerBase
    {
        [HttpGet("courses/{courseId:int}/grades/excel")]
        public async Task<IActionResult> ExportCourseGradesExcel(int courseId, [FromQuery] string? semester = null)
        {
            var query = context.Enrollments
                .Where(e => e.CourseId == courseId)
                .Include(e => e.Student)
                .Include(e => e.Course)
                .Include(e => e.Grade)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(semester))
            {
                query = query.Where(e => e.Semester == semester);
            }

            var rows = await query.ToListAsync();
            if (rows.Count == 0)
            {
                return NotFound("No data found for report.");
            }

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Grades");
            ws.Cell(1, 1).Value = "StudentCode";
            ws.Cell(1, 2).Value = "FullName";
            ws.Cell(1, 3).Value = "CourseCode";
            ws.Cell(1, 4).Value = "CourseName";
            ws.Cell(1, 5).Value = "Semester";
            ws.Cell(1, 6).Value = "Midterm";
            ws.Cell(1, 7).Value = "Final";
            ws.Cell(1, 8).Value = "Attendance";
            ws.Cell(1, 9).Value = "Total";
            ws.Cell(1, 10).Value = "Published";

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var line = i + 2;
                ws.Cell(line, 1).Value = row.Student.StudentCode;
                ws.Cell(line, 2).Value = row.Student.FullName;
                ws.Cell(line, 3).Value = row.Course.CourseCode;
                ws.Cell(line, 4).Value = row.Course.CourseName;
                ws.Cell(line, 5).Value = row.Semester;
                ws.Cell(line, 6).Value = ScoreFormatting.Trunc2Nullable(row.Grade?.MidtermScore);
                ws.Cell(line, 7).Value = ScoreFormatting.Trunc2Nullable(row.Grade?.FinalScore);
                ws.Cell(line, 8).Value = ScoreFormatting.Trunc2Nullable(row.Grade?.AttendanceScore);
                ws.Cell(line, 9).Value = ScoreFormatting.Trunc2Nullable(row.Grade?.TotalScore);
                ws.Cell(line, 10).Value = row.Grade?.IsPublished == true ? "Yes" : "No";
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();
            var fileName = $"course_{courseId}_grades.xlsx";

            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}
