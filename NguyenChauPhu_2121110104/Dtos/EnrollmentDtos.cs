namespace NguyenChauPhu_2121110104.Dtos
{
    public record UpsertEnrollmentRequest(
        int StudentId,
        int CourseId,
        string Semester,
        string Status = "Active");
}
