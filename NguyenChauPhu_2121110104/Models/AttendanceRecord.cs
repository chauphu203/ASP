namespace NguyenChauPhu_2121110104.Models
{
    public class AttendanceRecord
    {
        public int RecordId { get; set; }
        public int SessionId { get; set; }
        public int StudentId { get; set; }
        public DateTime ScanTime { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Present";
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public AttendanceSession Session { get; set; } = null!;
        public User Student { get; set; } = null!;
    }
}
