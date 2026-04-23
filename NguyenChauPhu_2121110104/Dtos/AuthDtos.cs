namespace NguyenChauPhu_2121110104.Dtos
{
    public record LoginRequest(string Username, string Password);
    public record RegisterRequest(string Username, string Password, string FullName, string Email, string? StudentCode);
    public record LoginResponse(string AccessToken, string Username, IEnumerable<string> Roles);
    public record MeResponse(int UserId, string Username, string FullName, IEnumerable<string> Roles, IEnumerable<string> Permissions);
}
