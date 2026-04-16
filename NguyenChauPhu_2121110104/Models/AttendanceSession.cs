namespace NguyenChauPhu_2121110104.Models
{
    public class AttendanceSession
    {
        public int SessionId { get; set; }
        public int CourseId { get; set; }
        public int LecturerId { get; set; }
        public DateOnly SessionDate { get; set; }
        public TimeOnly? StartTime { get; set; }
        public string QRToken { get; set; } = string.Empty;
        public DateTime? TokenExpiry { get; set; }

        public Course Course { get; set; } = null!;
        public User Lecturer { get; set; } = null!;
        public ICollection<AttendanceRecord> Records { get; set; } = new List<AttendanceRecord>();
    }
}
