using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SFCDashboardMobile.Models;
using SFCDashboardMobile.Data;

namespace SFCDashboardMob.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Assumes you're using authentication
    public class RegisterController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public RegisterController(ApplicationDbContext context)
        {
            _context = context;
        }

        private static string ExtractServiceId(string email)
        {
            if (string.IsNullOrEmpty(email))
                return string.Empty;

            return email[..Math.Min(email.Length, 6)];
        }

        [HttpGet]
        public async Task<IActionResult> GetUserDetails()
        {
            var email = User.Identity?.Name ?? string.Empty;
            var serviceId = ExtractServiceId(email);

            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.ServiceId == serviceId);

            if (existingUser == null)
            {
                var name = User.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? string.Empty;
                existingUser = new SystemUser
                {
                    Name = name,
                    ServiceId = serviceId
                };
            }

            return Ok(existingUser);
        }

        [HttpPost]
        public async Task<IActionResult> RegisterOrUpdateUser([FromBody] SystemUser user)
        {
            var email = User.Identity?.Name ?? string.Empty;
            var serviceId = ExtractServiceId(email);

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.ServiceId == serviceId);

            if (existingUser != null)
            {
                existingUser.WorkGroupId = user.WorkGroupId;
                _context.Update(existingUser);
            }
            else
            {
                user.ServiceId = serviceId;
                user.Name = User.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? string.Empty;
                _context.Add(user);
            }

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "User registered/updated successfully." });
            }
            catch (DbUpdateException)
            {
                return StatusCode(500, "An error occurred while saving changes.");
            }
        }

        [HttpGet("workgroups")]
        public async Task<IActionResult> GetWorkGroups()
        {
            var workGroups = await _context.WorkGroups
                .Select(wg => new { wg.Id, wg.Name })
                .ToListAsync();

            return Ok(workGroups);
        }
    }
}
