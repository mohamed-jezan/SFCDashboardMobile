using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SFCDashboardMobile.Data;
using SFCDashboardMobile.Models;

namespace SFCDashboardMobile.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PETaskListsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PETaskListsController> _logger;

        public PETaskListsController(ApplicationDbContext context, ILogger<PETaskListsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/PETaskLists
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PETaskList>>> GetPETaskLists()
        {
            return await _context.PETaskLists.ToListAsync();
        }

        // GET: api/PETaskLists/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PETaskList>> GetPETaskList(int id)
        {
            var task = await _context.PETaskLists.FindAsync(id);

            if (task == null)
                return NotFound();

            return task;
        }

        // POST: api/PETaskLists
        [HttpPost]
        public async Task<ActionResult<PETaskList>> CreatePETaskList(PETaskList pETaskList)
        {
            _context.PETaskLists.Add(pETaskList);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPETaskList), new { id = pETaskList.Id }, pETaskList);
        }

        // PUT: api/PETaskLists/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePETaskList(int id, PETaskList pETaskList)
        {
            if (id != pETaskList.Id)
                return BadRequest();

            _context.Entry(pETaskList).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PETaskListExists(id))
                    return NotFound();
                else
                    throw;
            }

            return NoContent();
        }

        // DELETE: api/PETaskLists/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePETaskList(int id)
        {
            var task = await _context.PETaskLists.FindAsync(id);
            if (task == null)
                return NotFound();

            _context.PETaskLists.Remove(task);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool PETaskListExists(int id)
        {
            return _context.PETaskLists.Any(e => e.Id == id);
        }
    }
}
