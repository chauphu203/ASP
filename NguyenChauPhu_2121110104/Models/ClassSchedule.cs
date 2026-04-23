namespace NguyenChauPhu_2121110104.Models
{
    public class ClassSchedule
    {
        public int ClassScheduleId { get; set; }
        public int CourseId { get; set; }
        public int LecturerId { get; set; }
        public string Room { get; set; } = string.Empty;
        public string DayOfWeek { get; set; } = string.Empty;
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }

        public Course Course { get; set; } = null!;
        public User Lecturer { get; set; } = null!;
    }
}
