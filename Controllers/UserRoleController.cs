using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SFCDashboardMobile.Data;

namespace SFCDashboardMobile.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SystemUsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SystemUsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/SystemUsers
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var users = await _context.Users
                .Include(u => u.UserRole)
                .Include(u => u.WorkGroup)
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.ServiceId,
                    UserRole = new { u.UserRole.Id, u.UserRole.Name },
                    WorkGroup = new { u.WorkGroup.Id, u.WorkGroup.Name }
                })
                .ToListAsync();

            return Ok(users);
        }

        // GET: api/SystemUsers/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var user = await _context.Users
                .Include(u => u.UserRole)
                .Include(u => u.WorkGroup)
                .Where(u => u.Id == id)
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.ServiceId,
                    UserRole = new { u.UserRole.Id, u.UserRole.Name },
                    WorkGroup = new { u.WorkGroup.Id, u.WorkGroup.Name }
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound();

            return Ok(user);
        }

        // POST: api/SystemUsers
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] SystemUser systemUser)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _context.Users.Add(systemUser);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = systemUser.Id }, systemUser);
        }

        // PUT: api/SystemUsers/5
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Put(int id, [FromBody] SystemUser systemUser)
        {
            if (id != systemUser.Id)
                return BadRequest("ID mismatch");

            var existingUser = await _context.Users.FindAsync(id);
            if (existingUser == null)
                return NotFound();

            existingUser.Name = systemUser.Name;
            existingUser.ServiceId = systemUser.ServiceId;
            existingUser.UserRoleId = systemUser.UserRoleId;
            existingUser.WorkGroupId = systemUser.WorkGroupId;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/SystemUsers/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/SystemUsers/SearchWorkgroups?term=abc
        [HttpGet("SearchWorkgroups")]
        public IActionResult SearchWorkgroups(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return Ok(new List<object>());

            var workgroups = _context.WorkGroups
                .Where(w => w.Name.Contains(term))
                .Select(w => new { w.Id, w.Name })
                .Take(20)
                .ToList();

            return Ok(workgroups);
        }

        // GET: api/SystemUsers/GetAll
        [HttpGet("GetAll")]
        public IActionResult GetAll()
        {
            var users = _context.Users
                .Select(u => new { u.Id, u.Name })
                .ToList();

            return Ok(users);
        }

        // GET: api/SystemUsers/GetCurrentUser
        [HttpGet("GetCurrentUser")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var serviceId = User.Identity?.Name;

            if (string.IsNullOrEmpty(serviceId))
                return Ok(null);

            var serviceIdShort = serviceId.Length > 6 ? serviceId.Substring(0, 6) : serviceId;

            var user = await _context.Users
                .Where(u => u.ServiceId == serviceIdShort)
                .Select(u => new { u.Id, u.Name })
                .FirstOrDefaultAsync();

            return Ok(user);
        }
    }
}
