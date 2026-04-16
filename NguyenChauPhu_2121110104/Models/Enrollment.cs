namespace NguyenChauPhu_2121110104.Models
{
    public class Enrollment
    {
        public int EnrollmentId { get; set; }
        public int StudentId { get; set; }
        public int CourseId { get; set; }
        public string Semester { get; set; } = string.Empty;
        public DateTime EnrollmentDate { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Active";

        public User Student { get; set; } = null!;
        public Course Course { get; set; } = null!;
        public Grade? Grade { get; set; }
    }
}
