using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;

namespace AcadSync.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _users;
        private readonly SignInManager<IdentityUser> _signin;

        public AuthController(UserManager<IdentityUser> users, SignInManager<IdentityUser> signin)
        {
            _users = users;
            _signin = signin;
        }

        public class RegisterRequest
        {
            public string UserName { get; set; } = null!;
            public string Password { get; set; } = null!;
        }

        public class LoginRequest
        {
            public string UserName { get; set; } = null!;
            public string Password { get; set; } = null!;
            public bool RememberMe { get; set; } = false;
        }

        [HttpPost("register")]
        [Authorize] // only existing authenticated users can create new accounts
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest("Username and password are required.");

            var user = new IdentityUser(req.UserName);
            var res = await _users.CreateAsync(user, req.Password);
            if (!res.Succeeded) return BadRequest(res.Errors);

            return Ok(new { message = "User registered" });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest("Username and password are required.");

            var result = await _signin.PasswordSignInAsync(req.UserName, req.Password, req.RememberMe, lockoutOnFailure: false);
            if (!result.Succeeded)
                return Unauthorized(new { message = "Invalid credentials" });

            return Ok(new { message = "Signed in" });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signin.SignOutAsync();
            return Ok(new { message = "Signed out" });
        }
    }
}
