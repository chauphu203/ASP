namespace NguyenChauPhu_2121110104.Models
{
    public class Grade
    {
        public int GradeId { get; set; }
        public int EnrollmentId { get; set; }
        public double? MidtermScore { get; set; }
        public double? FinalScore { get; set; }
        public double? AttendanceScore { get; set; }
        public double? TotalScore { get; set; }
        public double? GpaContribution { get; set; }
        public bool IsPublished { get; set; }

        public Enrollment Enrollment { get; set; } = null!;
    }
}
