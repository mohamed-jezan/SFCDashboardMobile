using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SFCDashboardMobile.Data;
using SFCDashboardMobile.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SFCDashboardMobile.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PETasksController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PETasksController> _logger;

        public PETasksController(ApplicationDbContext context, ILogger<PETasksController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET api/petasks/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<PETask>> GetTaskDetails(int id)
        {
            var task = await _context.PETasks
                .Include(t => t.PlannedEvent)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
                return NotFound();

            return Ok(task);
        }

        // POST api/petasks/{id}/markurgent
        [HttpPost("{id}/markurgent")]
        public async Task<IActionResult> MarkAsUrgent(int id)
        {
            var task = await _context.PETasks
                .Include(t => t.PlannedEvent)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null || task.TaskStatus?.ToUpper() != "ONGOING")
                return NotFound();

            task.IsUrgent = true;
            task.UrgentRequested = false;
            task.Priority = (task.Priority ?? "") + " [URGENT]";

            if (task.PlannedEvent != null)
            {
                task.PlannedEvent.PEStatus = "urgent";
                _context.Update(task.PlannedEvent);
            }

            _context.Update(task);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Task ID {taskId} marked as urgent directly", id);

            return Ok(new { message = "Task marked as urgent", taskId = id });
        }

        // GET api/petasks/urgentrequests
        [HttpGet("urgentrequests")]
        public async Task<ActionResult<IEnumerable<PETask>>> GetUrgentRequests()
        {
            var requests = await _context.PETasks
                .Where(t => t.UrgentRequested && !t.IsUrgent)
                .Include(t => t.PlannedEvent)
                .OrderBy(t => t.PENumber)
                .ToListAsync();

            return Ok(requests);
        }

        // You can convert other methods similarly...

    }
}
