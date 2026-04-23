namespace NguyenChauPhu_2121110104.Models
{
    public class ExamSchedule
    {
        public int ExamScheduleId { get; set; }
        public int CourseId { get; set; }
        public int LecturerId { get; set; }
        public DateOnly ExamDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public string Room { get; set; } = string.Empty;
        public string ExamType { get; set; } = "Final";

        public Course Course { get; set; } = null!;
        public User Lecturer { get; set; } = null!;
    }
}
