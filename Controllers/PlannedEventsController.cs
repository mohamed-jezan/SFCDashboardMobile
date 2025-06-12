using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SFCDashboardMobile.Data;
using SFCDashboardMobile.Models;
using System.Text.Json;

namespace SFCDashboard.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlannedEventsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PlannedEventsController> _logger;

        public PlannedEventsController(ApplicationDbContext context, ILogger<PlannedEventsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/PlannedEvents
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PlannedEvent>>> GetPlannedEvents(
            string searchType, string peNumber, string customer,
            string jobReference, string soNumber, int? workgroupId, int pageIndex = 1, int pageSize = 10, string searchString = null)
        {
            // Trim all search parameters
            peNumber = peNumber?.Trim();
            customer = customer?.Trim();
            jobReference = jobReference?.Trim();
            soNumber = soNumber?.Trim();

            // Get current user's workgroup info
            var (userWorkgroupId, canViewAll) = await GetCurrentUserWorkGroupAsync();

            // Base query
            var query = _context.PlannedEvents.AsQueryable();

            // Apply workgroup filtering
            if (canViewAll && workgroupId.HasValue)
            {
                var selectedWorkgroup = await _context.WorkGroups.FindAsync(workgroupId);
                if (selectedWorkgroup != null)
                {
                    query = query.Where(p => p.TaskWg != null && p.TaskWg.Contains(selectedWorkgroup.Name));
                }
            }
            else if (!canViewAll)
            {
                var workgroup = await _context.WorkGroups.FindAsync(userWorkgroupId);
                if (workgroup != null)
                {
                    query = query.Where(p => p.TaskWg != null && p.TaskWg.Contains(workgroup.Name));
                }
            }

            // Apply search filters
            if (!string.IsNullOrEmpty(searchString))
            {
                switch (searchType)
                {
                    case "customer":
                        query = query.Where(p => p.Customer != null && EF.Functions.Like(p.Customer, $"%{customer}%"));
                        break;
                    case "jobReference":
                        query = query.Where(p => p.JobReference != null && EF.Functions.Like(p.JobReference, $"%{jobReference}%"));
                        break;
                    case "soNumber":
                        query = query.Where(p => p.SoNumber != null && EF.Functions.Like(p.SoNumber, $"%{soNumber}%"));
                        break;
                    default: // peNumber
                        query = query.Where(p => p.PeNumber != null && EF.Functions.Like(p.PeNumber, $"%{peNumber}%"));
                        break;
                }
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(p => p.PECreatedDate)
                .ThenBy(p => p.PeNumber)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Get related tasks for the planned events
            var peNumbers = items.Select(pe => pe.PeNumber).ToList();
            var tasks = await _context.PETasks
                .Where(t => peNumbers.Contains(t.PENumber))
                .OrderBy(t => t.TaskSeq)
                .ToListAsync();

            var result = new
            {
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize,
                Items = items,
                Tasks = tasks.GroupBy(t => t.PENumber).ToDictionary(g => g.Key, g => g.ToList())
            };

            return Ok(result);
        }

        // GET: api/PlannedEvents/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PlannedEvent>> GetPlannedEvent(int id)
        {
            var plannedEvent = await _context.PlannedEvents.FindAsync(id);
            if (plannedEvent == null)
            {
                return NotFound();
            }

            // Get related tasks
            var tasks = await _context.PETasks
                .Where(t => t.PENumber == plannedEvent.PeNumber)
                .OrderBy(t => t.TaskSeq)
                .ToListAsync();

            // Get related issues
            var issues = await _context.PEIssues
                .Where(i => i.PlannedEventId == plannedEvent.Id)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            var result = new
            {
                PlannedEvent = plannedEvent,
                Tasks = tasks,
                Issues = issues
            };

            return Ok(result);
        }

        // POST: api/PlannedEvents
        [HttpPost]
        public async Task<ActionResult<PlannedEvent>> PostPlannedEvent(PlannedEvent plannedEvent)
        {
            _context.PlannedEvents.Add(plannedEvent);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetPlannedEvent", new { id = plannedEvent.Id }, plannedEvent);
        }

        // PUT: api/PlannedEvents/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPlannedEvent(int id, PlannedEvent plannedEvent)
        {
            if (id != plannedEvent.Id)
            {
                return BadRequest();
            }

            _context.Entry(plannedEvent).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PlannedEventExists(id))
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

        // DELETE: api/PlannedEvents/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePlannedEvent(int id)
        {
            var plannedEvent = await _context.PlannedEvents.FindAsync(id);
            if (plannedEvent == null)
            {
                return NotFound();
            }

            _context.PlannedEvents.Remove(plannedEvent);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/PlannedEvents/in-progress
        [HttpGet("in-progress")]
        public async Task<ActionResult<IEnumerable<PlannedEvent>>> GetInProgressRecords(int? workgroupId)
        {
            var (userWorkgroupId, canViewAll) = await GetCurrentUserWorkGroupAsync();
            var effectiveWorkgroupId = canViewAll ? workgroupId : userWorkgroupId;

            // Get PE numbers with OLA violation
            var violatingPENumbers = await _context.PETasks
                .Where(t => t.IsOLAViolate)
                .Select(t => t.PENumber)
                .Distinct()
                .ToListAsync();

            // Base query, EXCLUDING OLA Violate records
            var query = _context.PlannedEvents
                .Where(p => (p.PEStatus == "ongoing" && p.IsHold == false || p.PEStatus == "PENDING_URGENT_CONFIRMATION")
                && !violatingPENumbers.Contains(p.PeNumber))
                .AsNoTracking();

            // Apply workgroup filter
            if (effectiveWorkgroupId.HasValue)
            {
                var workgroup = await _context.WorkGroups.FindAsync(effectiveWorkgroupId);
                if (workgroup != null)
                {
                    query = query.Where(p => p.TaskWg != null && EF.Functions.Like(p.TaskWg, $"%{workgroup.Name}%"));
                }
            }

            var records = await query.ToListAsync();
            return Ok(records);
        }

        // GET: api/PlannedEvents/ola-violations
        [HttpGet("ola-violations")]
        public async Task<ActionResult<IEnumerable<PlannedEvent>>> GetOLAViolations(int? workgroupId)
        {
            var (userWorkgroupId, canViewAll) = await GetCurrentUserWorkGroupAsync();
            workgroupId = workgroupId ?? userWorkgroupId;

            // Get PE numbers with OLA violation
            var violatingPENumbers = await _context.PETasks
                .Where(t => t.IsOLAViolate)
                .Select(t => t.PENumber)
                .Distinct()
                .ToListAsync();

            // Get the PE records with at least one OLA-violated task
            var query = _context.PlannedEvents
                .Where(p => violatingPENumbers.Contains(p.PeNumber));

            // Apply workgroup filter if needed
            if (workgroupId.HasValue)
            {
                var workgroup = await _context.WorkGroups.FindAsync(workgroupId);
                if (workgroup != null)
                {
                    query = query.Where(p => p.TaskWg != null && p.TaskWg.Contains(workgroup.Name));
                }
            }

            var olaViolateRecords = await query.OrderBy(p => p.PeNumber).ToListAsync();
            return Ok(olaViolateRecords);
        }

        // GET: api/PlannedEvents/hold
        [HttpGet("hold")]
        public async Task<ActionResult<IEnumerable<PlannedEvent>>> GetHoldRecords(string peNumber, string reference, string customer, int? workgroupId)
        {
            var (userWorkgroupId, canViewAll) = await GetCurrentUserWorkGroupAsync();

            // Start with base query that explicitly filters for IsHold = true
            var query = _context.PlannedEvents.Where(p => p.IsHold == true);

            // Apply workgroup filtering
            if (canViewAll && workgroupId.HasValue)
            {
                var selectedWorkgroup = await _context.WorkGroups.FindAsync(workgroupId);
                if (selectedWorkgroup != null)
                {
                    query = query.Where(p => p.TaskWg != null && p.TaskWg.Contains(selectedWorkgroup.Name));
                }
            }
            else if (!canViewAll)
            {
                // Regular users can only see their own workgroup's records
                var workgroup = await _context.WorkGroups.FindAsync(userWorkgroupId);
                if (workgroup != null)
                {
                    query = query.Where(p => p.TaskWg != null && p.TaskWg.Contains(workgroup.Name));
                }
            }

            // Apply search filters
            if (!string.IsNullOrEmpty(peNumber))
            {
                query = query.Where(p => p.PeNumber.Contains(peNumber));
            }

            if (!string.IsNullOrEmpty(reference))
            {
                query = query.Where(p => p.RequestReferenceNo != null && p.RequestReferenceNo.Contains(reference));
            }

            if (!string.IsNullOrEmpty(customer))
            {
                query = query.Where(p => p.Customer != null && p.Customer.Contains(customer));
            }

            var holdRecords = await query.ToListAsync();
            return Ok(holdRecords);
        }

        // GET: api/PlannedEvents/urgent
        [HttpGet("urgent")]
        public async Task<ActionResult<IEnumerable<PlannedEvent>>> GetUrgentRecords(int? workgroupId)
        {
            // Get PE numbers with OLA violation
            var violatingPENumbers = await _context.PETasks
                .Where(t => t.IsOLAViolate)
                .Select(t => t.PENumber)
                .Distinct()
                .ToListAsync();

            var query = _context.PlannedEvents
                .Where(p => p.PEStatus == "urgent" && p.IsHold == false && !violatingPENumbers.Contains(p.PeNumber));

            // Apply workgroup filter if selected
            if (workgroupId.HasValue)
            {
                var workgroup = await _context.WorkGroups.FindAsync(workgroupId);
                if (workgroup != null)
                {
                    query = query.Where(p => p.TaskWg != null && p.TaskWg.Contains(workgroup.Name));
                }
            }

            var urgentRecords = await query.ToListAsync();
            return Ok(urgentRecords);
        }

        // GET: api/PlannedEvents/urgent-requests
        [HttpGet("urgent-requests")]
        public async Task<ActionResult<IEnumerable<PlannedEvent>>> GetPendingUrgentRequests()
        {
            var pendingRequests = await _context.PlannedEvents
                .Where(p => p.PEStatus == "PENDING_URGENT_CONFIRMATION")
                .OrderBy(p => p.PeNumber)
                .ToListAsync();

            return Ok(pendingRequests);
        }

        // POST: api/PlannedEvents/5/process-urgent-request
        [HttpPost("{id}/process-urgent-request")]
        public async Task<IActionResult> ProcessUrgentRequest(int id, [FromBody] string urgentReason)
        {
            var plannedEvent = await _context.PlannedEvents.FindAsync(id);
            if (plannedEvent == null || plannedEvent.PEStatus == "COMPLETED")
            {
                return NotFound();
            }

            bool markAsUrgent = false;
            string priorityMessage = "";
            int priorityLevel = 0;

            switch (urgentReason)
            {
                case "OpeningCeremony":
                    markAsUrgent = true;
                    plannedEvent.PEStatus = "URGENT";
                    priorityMessage = "[URGENT: Opening Ceremony - Priority 1]";
                    priorityLevel = 1;
                    plannedEvent.Priority = priorityMessage;
                    break;

                case "CriticalCustomer":
                    markAsUrgent = true;
                    plannedEvent.PEStatus = "URGENT";
                    priorityMessage = "[URGENT: Critical Customer - Priority 2]";
                    priorityLevel = 2;
                    plannedEvent.Priority = priorityMessage;
                    break;

                case "Reject":
                    plannedEvent.PEStatus = "ongoing";
                    plannedEvent.Priority = "Urgent Request Rejected";
                    break;

                default:
                    return BadRequest("Invalid option selected.");
            }

            _context.Update(plannedEvent);

            // If PE was marked as urgent, update all its tasks to be urgent as well
            if (markAsUrgent)
            {
                var relatedTasks = await _context.PETasks
                    .Where(t => t.PENumber == plannedEvent.PeNumber)
                    .ToListAsync();

                foreach (var task in relatedTasks)
                {
                    task.IsUrgent = true;
                    task.UrgentRequested = false;
                    task.Priority = priorityMessage + " (Inherited from PE)";
                }

                if (relatedTasks.Any())
                {
                    _context.UpdateRange(relatedTasks);
                }
            }

            await _context.SaveChangesAsync();

            var response = new
            {
                Success = true,
                Message = markAsUrgent
                    ? $"Planned Event marked as urgent with priority {priorityLevel}. All related tasks have also been marked as urgent."
                    : "Urgent request processed.",
                PlannedEventId = plannedEvent.Id
            };

            return Ok(response);
        }

        // POST: api/PlannedEvents/5/request-urgent
        [HttpPost("{id}/request-urgent")]
        public async Task<IActionResult> RequestUrgentWithReason(int id, [FromBody] string urgentReason)
        {
            var plannedEvent = await _context.PlannedEvents.FindAsync(id);
            if (plannedEvent == null || plannedEvent.PEStatus?.ToUpper() == "COMPLETED" || plannedEvent.PEStatus?.ToUpper() == "URGENT")
            {
                return NotFound();
            }

            // Update PE status and priority based on the selected reason
            switch (urgentReason)
            {
                case "OpeningCeremony":
                    plannedEvent.PEStatus = "PENDING_URGENT_CONFIRMATION";
                    plannedEvent.Priority = (plannedEvent.Priority ?? "") + " [URGENT REQUEST PENDING: Opening Ceremony]";
                    break;

                case "CriticalCustomer":
                    plannedEvent.PEStatus = "PENDING_URGENT_CONFIRMATION";
                    plannedEvent.Priority = (plannedEvent.Priority ?? "") + " [URGENT REQUEST PENDING: Critical Customer]";
                    break;

                default:
                    return BadRequest("Invalid urgency reason selected.");
            }

            _context.Update(plannedEvent);
            await _context.SaveChangesAsync();

            var response = new
            {
                Success = true,
                Message = "Urgent request submitted for approval.",
                PlannedEventId = plannedEvent.Id
            };

            return Ok(response);
        }

        // POST: api/PlannedEvents/5/mark-ola-urgent
        [HttpPost("{id}/mark-ola-urgent")]
        public async Task<IActionResult> MarkOLARecordUrgent(int id)
        {
            var plannedEvent = await _context.PlannedEvents.FindAsync(id);

            if (plannedEvent == null)
            {
                return NotFound("Record not found.");
            }

            var currentDate = DateTime.Today;

            // Find the violating tasks for this PE
            var violatingTasks = await _context.PETasks
                .Where(t => t.PENumber == plannedEvent.PeNumber &&
                          t.TaskStatus != "COMPLETED" &&
                          t.TaskCompleteDate.Date < currentDate)
                .OrderBy(t => t.TaskCompleteDate)  // Start with the most overdue
                .ToListAsync();

            if (violatingTasks.Any())
            {
                // Mark the first/most overdue violating task as urgent
                var mostOverdueTask = violatingTasks.First();
                mostOverdueTask.IsUrgent = true;
                mostOverdueTask.Priority = (mostOverdueTask.Priority ?? "") + " [URGENT: OLA VIOLATED]";
                _context.Update(mostOverdueTask);

                // Update PE status to urgent
                plannedEvent.PEStatus = "urgent";
                _context.Update(plannedEvent);

                await _context.SaveChangesAsync();

                var response = new
                {
                    Success = true,
                    Message = $"PE {plannedEvent.PeNumber} marked as urgent due to OLA violation.",
                    PlannedEventId = plannedEvent.Id,
                    TaskId = mostOverdueTask.Id
                };

                return Ok(response);
            }
            else
            {
                return NotFound("No violating tasks found for this PE.");
            }
        }

        // GET: api/PlannedEvents/dashboard-counts
        [HttpGet("dashboard-counts")]
        public async Task<ActionResult<object>> GetDashboardCounts(int? workgroupId)
        {
            var (userWorkgroupId, canViewAll) = await GetCurrentUserWorkGroupAsync();
            var effectiveWorkgroupId = canViewAll ? workgroupId : userWorkgroupId;

            var urgentCount = await GetUrgentCount(effectiveWorkgroupId);
            var inProgressCount = await GetInProgressCount(effectiveWorkgroupId);
            var olaViolateCount = await GetOLAViolateCount(effectiveWorkgroupId);
            var holdCount = await GetHoldCount(effectiveWorkgroupId);

            return new
            {
                UrgentCount = urgentCount,
                InProgressCount = inProgressCount,
                OLAViolateCount = olaViolateCount,
                HoldCount = holdCount
            };
        }

        // GET: api/PlannedEvents/5/basic-details
        [HttpGet("{id}/basic-details")]
        public async Task<ActionResult<object>> GetBasicDetails(int id)
        {
            var plannedEvent = await _context.PlannedEvents.FindAsync(id);
            if (plannedEvent == null)
            {
                return NotFound();
            }

            return new
            {
                peNumber = plannedEvent.PeNumber,
                customer = plannedEvent.Customer,
                peStatus = plannedEvent.PEStatus,
                serviceType = plannedEvent.ServiceType,
                taskName = plannedEvent.TaskName,
                taskWg = plannedEvent.TaskWg
            };
        }

        private bool PlannedEventExists(int id)
        {
            return _context.PlannedEvents.Any(e => e.Id == id);
        }

        private async Task<(int? workgroupId, bool canViewAll)> GetCurrentUserWorkGroupAsync()
        {
            var serviceId = User.Identity?.Name;
            if (string.IsNullOrEmpty(serviceId))
                return (null, false);

            // Extract the substring before the query (first 6 characters)
            var serviceIdShort = serviceId.Length > 6 ? serviceId.Substring(0, 6) : serviceId;

            var user = await _context.Users
                .Include(u => u.WorkGroup)
                .FirstOrDefaultAsync(u => u.ServiceId == serviceIdShort);

            bool canViewAll = user?.WorkGroup?.Name == "ALL-WORKGROUPS";
            return (user?.WorkGroupId, canViewAll);
        }

        private async Task<int> GetUrgentCount(int? workgroupId)
        {
            // Get PE numbers with OLA violation
            var violatingPENumbers = await _context.PETasks
                .Where(t => t.IsOLAViolate)
                .Select(t => t.PENumber)
                .Distinct()
                .ToListAsync();

            var query = _context.PlannedEvents
                .Where(p => p.PEStatus == "urgent" && p.IsHold == false && !violatingPENumbers.Contains(p.PeNumber));

            if (workgroupId.HasValue)
            {
                var workgroup = await _context.WorkGroups.FindAsync(workgroupId);
                if (workgroup != null)
                {
                    query = query.Where(p => p.TaskWg != null && EF.Functions.Like(p.TaskWg, $"%{workgroup.Name}%"));
                }
            }

            return await query.CountAsync();
        }

        private async Task<int> GetOLAViolateCount(int? workgroupId)
        {
            var query = _context.PETasks.Where(p => p.IsOLAViolate);

            if (workgroupId.HasValue)
            {
                var workgroup = await _context.WorkGroups.FindAsync(workgroupId);
                if (workgroup != null)
                {
                    query = query.Where(p => p.TaskWorkGroup != null && p.TaskWorkGroup.Contains(workgroup.Name));
                }
            }

            // Count unique PE Numbers that have at least one violating task
            return await query.Select(p => p.PENumber).Distinct().CountAsync();
        }

        private async Task<int> GetHoldCount(int? workgroupId)
        {
            var query = _context.PlannedEvents.Where(p => p.IsHold == true);

            if (workgroupId.HasValue)
            {
                var workgroup = await _context.WorkGroups.FindAsync(workgroupId);
                if (workgroup != null)
                {
                    query = query.Where(p => p.TaskWg != null && EF.Functions.Like(p.TaskWg, $"%{workgroup.Name}%"));
                }
            }

            return await query.CountAsync();
        }

        private async Task<int> GetInProgressCount(int? workgroupId)
        {
            // Get PE numbers with OLA violation
            var violatingPENumbers = await _context.PETasks
                .Where(t => t.IsOLAViolate)
                .Select(t => t.PENumber)
                .Distinct()
                .ToListAsync();

            var query = _context.PlannedEvents
                .Where(p => (p.PEStatus == "ongoing" && p.IsHold == false || p.PEStatus == "PENDING_URGENT_CONFIRMATION") &&
                    !violatingPENumbers.Contains(p.PeNumber));

            if (workgroupId.HasValue)
            {
                var workgroup = await _context.WorkGroups.FindAsync(workgroupId);
                if (workgroup != null)
                {
                    query = query.Where(p => p.TaskWg != null && EF.Functions.Like(p.TaskWg, $"%{workgroup.Name}%"));
                }
            }

            return await query.CountAsync();
        }
    }
}