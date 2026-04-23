namespace NguyenChauPhu_2121110104.Dtos
{
    public record CreateUserRequest(
        string Username,
        string Password,
        string FullName,
        string Email,
        string? StudentCode,
        string? LecturerCode,
        List<string> Roles);

    public record CreateStudentItem(
        string StudentCode,
        string FullName,
        string Email);

    public record CreateStudentsBulkRequest(
        List<CreateStudentItem> Students,
        string DefaultPassword);

    public record UpdateMyProfileRequest(
        string FullName,
        string Email,
        string? Password);
}
