using System; using System.IdentityModel.Tokens.Jwt; using System.Security.Claims; using System.Text; using System.Threading.Tasks; using Microsoft.AspNetCore.Identity; using Microsoft.AspNetCore.Mvc; using Microsoft.Extensions.Configuration; using Microsoft.IdentityModel.Tokens; using UIStore.Models;
namespace UIStore.Controllers.Api {
    [ApiController] [Route("api/v1/auth")]
    public class AuthApiController : ControllerBase {
        private readonly UserManager<ApplicationUser> _um; private readonly SignInManager<ApplicationUser> _sm; private readonly IConfiguration _cfg;
        public AuthApiController(UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm, IConfiguration cfg) { _um = um; _sm = sm; _cfg = cfg; }

        /// <summary>使用帳號密碼取得 JWT Token</summary>
        [HttpPost("token")]
        [Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryToken]
        public async Task<IActionResult> GetToken([FromBody] ApiLoginRequest req) {
            if (req == null || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { success = false, message = "Email 和 Password 為必填" });
            var user = await _um.FindByEmailAsync(req.Email);
            if (user == null) return Unauthorized(new { success = false, message = "帳號或密碼錯誤" });
            var result = await _sm.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
            if (!result.Succeeded) return Unauthorized(new { success = false, message = result.IsLockedOut ? "帳號已鎖定" : "帳號或密碼錯誤" });
            var roles = await _um.GetRolesAsync(user);
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"] ?? "UIStore_DefaultSecretKey_2026_ChangeThisInProduction!"));
            var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id), new Claim(JwtRegisteredClaimNames.Email, user.Email), new Claim("fullName", user.FullName ?? ""), new Claim(ClaimTypes.Role, string.Join(",", roles)) };
            var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddDays(7), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
            return Ok(new { success = true, token = new JwtSecurityTokenHandler().WriteToken(token), expires = token.ValidTo, roles });
        }
    }

    public class ApiLoginRequest { public string Email { get; set; } public string Password { get; set; } }
}
