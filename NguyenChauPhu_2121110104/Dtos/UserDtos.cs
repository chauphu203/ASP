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
}
