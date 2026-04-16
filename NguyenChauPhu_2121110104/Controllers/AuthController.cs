using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104.Data;
using NguyenChauPhu_2121110104.Dtos;
using NguyenChauPhu_2121110104.Services;

namespace NguyenChauPhu_2121110104.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController(AppDbContext context, JwtTokenService tokenService) : ControllerBase
    {
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
        {
            var user = await context.Users.FirstOrDefaultAsync(x => x.Username == request.Username);
            if (user is null || !user.IsActive)
            {
                return Unauthorized("Invalid username or account disabled.");
            }

            var ok = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!ok)
            {
                return Unauthorized("Invalid username or password.");
            }

            user.LastLogin = DateTime.UtcNow;
            await context.SaveChangesAsync();

            var roles = await context.UserRoles
                .Where(ur => ur.UserId == user.UserId)
                .Select(ur => ur.Role.RoleName)
                .ToListAsync();

            var token = tokenService.GenerateToken(user, roles);
            return Ok(new LoginResponse(token, user.Username, roles));
        }
    }
}
