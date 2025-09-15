using Microsoft.AspNetCore.Mvc;
using LoginApi.Data;
using LoginApi.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LoginApi.Models;

namespace LoginApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest login)
    {
        var user = _context.Users.FirstOrDefault(u => u.Username == login.Username);
        if (user == null || user.PasswordHash != login.Password)
            return Unauthorized();

        var key = Encoding.ASCII.GetBytes(_config["Jwt:Key"] ?? "default_key");
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
        Subject = new ClaimsIdentity(new[] { new Claim("username", user.Username) }),
        Expires = DateTime.UtcNow.AddHours(1),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return Ok(new { token = tokenHandler.WriteToken(token) });
    }
    }
}