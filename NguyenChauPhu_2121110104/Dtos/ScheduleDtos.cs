namespace NguyenChauPhu_2121110104.Dtos
{
    /// <summary>Mở lớp tối giản: tạo một dòng lịch để môn xuất hiện trong danh sách SV tự đăng ký (không cần chọn SV).</summary>
    public record QuickOpenClassRequest(int CourseId, int? LecturerId);

    public record CreateClassScheduleRequest(
        int CourseId,
        int LecturerId,
        string Room,
        string DayOfWeek,
        TimeOnly StartTime,
        TimeOnly EndTime,
        DateOnly StartDate,
        DateOnly EndDate);

    public record CreateExamScheduleRequest(
        int CourseId,
        int LecturerId,
        DateOnly ExamDate,
        TimeOnly StartTime,
        TimeOnly EndTime,
        string Room,
        string ExamType);
}
