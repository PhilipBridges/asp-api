using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TodoApi.Dto;
using TodoApi.Models;

namespace TodoApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    public class UsersController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly IMemoryCache _cache;
        private readonly TodoApiContext _context;

        public UsersController(TodoApiContext context, ILogger<UsersController> logger, IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }

        // GET: api/Users
        [HttpGet]
        [AllowAnonymous]
        public IEnumerable<User> GetUser()
        {
            return _context.User;
        }

        // GET: api/Users/5
        [HttpGet("{id}")]
        [Authorize("test")]
        public async Task<IActionResult> GetUser([FromRoute] long id)
        {

            //if (!ModelState.IsValid)
            //{
            //    return BadRequest(ModelState);
            //}

            var user = await _context.User.FindAsync(id);

            if (user == null)
            {
                return NotFound(user);
            }

            return Ok(user);
        }

        // PUT: api/Users/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser([FromRoute] int id, [FromBody] User user)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != user.Id)
            {
                return BadRequest();
            }

            _context.Entry(user).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Users
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> PostUser([FromBody] User user)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var salt = Salt.Create();
            var hashedPassword = Hash.Create(user.password);
            if (string.IsNullOrEmpty(hashedPassword))
            {
                return BadRequest();
            }
            user.password = hashedPassword;
            _context.User.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetUser", new { id = user.Id }, user);
        }

        // POST: api/Users/login
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto logindata)
        {
            IQueryable<User> userQuery = from u in _context.User
                                         where u.email.Equals(logindata.email)
                                         select u;

            User dbUser = userQuery.FirstOrDefault<User>();

            var match = Hash.Validate(logindata.password, dbUser.password);

            if (match)
            {

                var claims = new List<Claim>
                {
                  new Claim(ClaimTypes.Name, dbUser.Id.ToString()),
                  new Claim("access_token", GetAccessToken(dbUser.Id.ToString(), dbUser.name))
                };

                var claimsIdentity = new ClaimsIdentity(
                  claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties();

                await HttpContext.SignInAsync(
                  CookieAuthenticationDefaults.AuthenticationScheme,
                  new ClaimsPrincipal(claimsIdentity),
                  authProperties);

                LoginUserDto returnedUser = new LoginUserDto() { Id = dbUser.Id, name = dbUser.name };
                return CreatedAtAction("GetUser", claims);
            }

            return BadRequest(match);

        }

        // POST: api/Users/signout
        [HttpGet("signout")]
        [AllowAnonymous]
        public async Task<IActionResult> Signout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return Ok("Logged out.");
        }

        // DELETE: api/Users/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser([FromRoute] int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _context.User.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            _context.User.Remove(user);
            await _context.SaveChangesAsync();

            return Ok(user);
        }

        private static string GetAccessToken(string userId, string username)
        {
            const string issuer = "localhost";
            const string audience = "localhost";

            var identity = new ClaimsIdentity(new List<Claim>
            {
             new Claim("sub", userId),
             new Claim("username", username)
            });

            var bytes = Encoding.UTF8.GetBytes(userId + "supersecret");
            var key = new SymmetricSecurityKey(bytes);
            var signingCredentials = new SigningCredentials(
              key, SecurityAlgorithms.HmacSha256);

            var now = DateTime.UtcNow;
            var handler = new JwtSecurityTokenHandler();

            var token = handler.CreateJwtSecurityToken(
              issuer, audience, identity,
              now, now.Add(TimeSpan.FromHours(1)),
              now, signingCredentials);

            return handler.WriteToken(token);
        }

        public IActionResult Revoke()
        {
            var principal = HttpContext.User as ClaimsPrincipal;
            var userId = principal?.Claims
              .First(c => c.Type == ClaimTypes.Name);

            _cache.Set("revoke-" + userId.Value, true);

            return Ok("thing");
        }

        private bool UserExists(int id)
        {
            return _context.User.Any(e => e.Id == id);
        }
    }
}