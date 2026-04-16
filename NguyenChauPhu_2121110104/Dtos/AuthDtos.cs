namespace NguyenChauPhu_2121110104.Dtos
{
    public record LoginRequest(string Username, string Password);
    public record LoginResponse(string AccessToken, string Username, IEnumerable<string> Roles);
}
