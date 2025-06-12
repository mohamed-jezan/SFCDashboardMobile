using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SFCDashboardMobile.Data;
using SFCDashboardMobile.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SFCDashboardMobile.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IssuesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<IssuesApiController> _logger;

        public IssuesApiController(ApplicationDbContext context, IWebHostEnvironment env, ILogger<IssuesApiController> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        // GET: api/IssuesApi/Inbox
        [HttpGet("Inbox")]
        public async Task<ActionResult<IEnumerable<PEIssueViewModel>>> GetInboxIssues()
        {
            var currentUserId = await GetCurrentUserIdAsync();
            if (currentUserId == 0) return Unauthorized();

            var issues = await _context.PEIssues
                .Where(i => i.ReceiverId == currentUserId && !i.IsHiddenFromInbox && !i.IsResolved)
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new PEIssueViewModel
                {
                    Id = i.Id,
                    SenderId = i.SenderId,
                    SenderName = _context.Users
                        .Where(u => u.Id == i.SenderId)
                        .Select(u => u.Name)
                        .FirstOrDefault() ?? "Unknown",
                    ReceiverId = i.ReceiverId,
                    ReceiverName = _context.Users
                        .Where(u => u.Id == i.ReceiverId)
                        .Select(u => u.Name)
                        .FirstOrDefault() ?? "Unknown",
                    IssueText = i.IssueText,
                    AttachmentPath = i.AttachmentPath,
                    CreatedAt = i.CreatedAt,
                    PlannedEventId = i.PlannedEventId,
                    IsRead = i.IsRead,
                    IsReply = i.IsReply,
                    OriginalIssueId = i.OriginalIssueId,
                    IsResolved = i.IsResolved,
                    IsResolutionRequest = i.IsResolutionRequest,
                    PETaskId = i.PETaskId
                })
                .ToListAsync();

            return Ok(issues);
        }

        // GET: api/IssuesApi/Sent
        [HttpGet("Sent")]
        public async Task<ActionResult<IEnumerable<PEIssueViewModel>>> GetSentIssues()
        {
            var currentUserId = await GetCurrentUserIdAsync();
            if (currentUserId == 0) return Unauthorized();

            var issues = await _context.PEIssues
                .Where(i => i.SenderId == currentUserId)
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new PEIssueViewModel
                {
                    Id = i.Id,
                    SenderId = currentUserId,
                    SenderName = _context.Users
                        .Where(u => u.Id == currentUserId)
                        .Select(u => u.Name)
                        .FirstOrDefault() ?? "Unknown",
                    ReceiverId = i.ReceiverId,
                    ReceiverName = _context.Users
                        .Where(u => u.Id == i.ReceiverId)
                        .Select(u => u.Name)
                        .FirstOrDefault() ?? "Unknown",
                    IssueText = i.IssueText,
                    AttachmentPath = i.AttachmentPath,
                    CreatedAt = i.CreatedAt,
                    PlannedEventId = i.PlannedEventId,
                    IsRead = i.IsRead,
                    IsReply = i.IsReply,
                    OriginalIssueId = i.OriginalIssueId,
                    IsResolved = i.IsResolved,
                    IsResolutionRequest = i.IsResolutionRequest,
                    PETaskId = i.PETaskId
                })
                .ToListAsync();

            return Ok(issues);
        }

        // GET: api/IssuesApi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PEIssue>> GetIssue(int id)
        {
            var issue = await _context.PEIssues
                .Include(i => _context.Users.Where(u => u.Id == i.SenderId).Select(u => u.Name).FirstOrDefault())
                .Include(i => _context.Users.Where(u => u.Id == i.ReceiverId).Select(u => u.Name).FirstOrDefault())
                .FirstOrDefaultAsync(i => i.Id == id);

            if (issue == null)
            {
                return NotFound();
            }

            // Mark as read if receiver is viewing
            var currentUserId = await GetCurrentUserIdAsync();
            if (currentUserId != 0 && issue.ReceiverId == currentUserId && !issue.IsRead)
            {
                issue.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return Ok(issue);
        }

        // POST: api/IssuesApi
        [HttpPost]
        public async Task<ActionResult<PEIssue>> CreateIssue([FromForm] IssueCreateDto issueDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var currentUserId = await GetCurrentUserIdAsync();
            if (currentUserId == 0)
            {
                return Unauthorized("Unable to identify current user.");
            }

            var issue = new PEIssue
            {
                PlannedEventId = issueDto.PlannedEventId,
                PETaskId = issueDto.PETaskId,
                SenderId = currentUserId,
                ReceiverId = issueDto.ReceiverId,
                IssueText = issueDto.IssueText,
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            if (issueDto.Attachment != null && issueDto.Attachment.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "issues", issue.PlannedEventId.ToString());
                Directory.CreateDirectory(uploadsFolder);
                var uniqueFileName = $"{DateTime.Now:yyyyMMddHHmmss}_{Path.GetFileName(issueDto.Attachment.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await issueDto.Attachment.CopyToAsync(stream);
                }
                issue.AttachmentPath = $"/uploads/issues/{issue.PlannedEventId}/{uniqueFileName}";
            }

            try
            {
                _context.PEIssues.Add(issue);
                await _context.SaveChangesAsync();

                // Set the PE on hold
                var pe = await _context.PlannedEvents.FindAsync(issue.PlannedEventId);
                if (pe != null)
                {
                    pe.IsHold = true;
                    _context.Update(pe);
                    await _context.SaveChangesAsync();
                }

                return CreatedAtAction(nameof(GetIssue), new { id = issue.Id }, issue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating issue for user {UserId}", currentUserId);
                return StatusCode(500, "An error occurred while creating the issue.");
            }
        }

        // POST: api/IssuesApi/Reply
        [HttpPost("Reply")]
        public async Task<ActionResult<PEIssue>> ReplyToIssue([FromForm] IssueReplyDto replyDto)
        {
            var originalIssue = await _context.PEIssues.FindAsync(replyDto.IssueId);
            if (originalIssue == null)
            {
                return NotFound("Original issue not found");
            }

            var currentUserId = await GetCurrentUserIdAsync();
            if (currentUserId == 0)
            {
                return Unauthorized("Unable to identify current user.");
            }

            var reply = new PEIssue
            {
                PlannedEventId = originalIssue.PlannedEventId,
                PETaskId = originalIssue.PETaskId,
                SenderId = currentUserId,
                ReceiverId = originalIssue.SenderId,
                IssueText = replyDto.ReplyText,
                CreatedAt = DateTime.Now,
                IsRead = false,
                IsReply = true,
                OriginalIssueId = replyDto.IssueId
            };

            if (replyDto.Attachment != null && replyDto.Attachment.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "issues", reply.PlannedEventId.ToString());
                Directory.CreateDirectory(uploadsFolder);
                var uniqueFileName = $"{DateTime.Now:yyyyMMddHHmmss}_{Path.GetFileName(replyDto.Attachment.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await replyDto.Attachment.CopyToAsync(stream);
                }
                reply.AttachmentPath = $"/uploads/issues/{reply.PlannedEventId}/{uniqueFileName}";
            }

            _context.PEIssues.Add(reply);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetIssue), new { id = reply.Id }, reply);
        }

        // POST: api/IssuesApi/MarkAsRead/5
        [HttpPost("MarkAsRead/{id}")]
        public async Task<IActionResult> MarkIssueAsRead(int id)
        {
            var issue = await _context.PEIssues.FindAsync(id);
            if (issue == null)
            {
                return NotFound();
            }

            var currentUserId = await GetCurrentUserIdAsync();
            if (currentUserId == 0 || issue.ReceiverId != currentUserId)
            {
                return Unauthorized();
            }

            if (!issue.IsRead)
            {
                issue.IsRead = true;
                _context.Update(issue);
                await _context.SaveChangesAsync();
            }

            return NoContent();
        }

        // POST: api/IssuesApi/MarkAsFixed
        [HttpPost("MarkAsFixed")]
        public async Task<ActionResult<PEIssue>> MarkIssueAsFixed([FromBody] IssueResolutionDto resolutionDto)
        {
            var issue = await _context.PEIssues.FindAsync(resolutionDto.IssueId);
            if (issue == null)
            {
                return NotFound("Issue not found");
            }

            // Create a resolution confirmation request
            var resolutionRequest = new PEIssueResolution
            {
                IssueId = resolutionDto.IssueId,
                ResolutionDetails = resolutionDto.ResolutionDetails,
                ResolutionDate = DateTime.Now,
                IsConfirmed = false,
                ConfirmationRequestedDate = DateTime.Now,
                PlannedEventId = resolutionDto.PlannedEventId
            };

            _context.PEIssueResolutions.Add(resolutionRequest);

            // Send a notification to the original reporter
            var notification = new PEIssue
            {
                PlannedEventId = resolutionDto.PlannedEventId,
                PETaskId = issue.PETaskId,
                SenderId = issue.ReceiverId, // Current user who fixed it
                ReceiverId = issue.SenderId, // Original reporter
                IssueText = $"RESOLUTION REQUEST: {resolutionDto.ResolutionDetails}",
                CreatedAt = DateTime.Now,
                IsRead = false,
                IsReply = true,
                OriginalIssueId = resolutionDto.IssueId,
                IsResolutionRequest = true
            };

            _context.PEIssues.Add(notification);
            await _context.SaveChangesAsync();

            return Ok(notification);
        }

        // POST: api/IssuesApi/ConfirmResolution
        [HttpPost("ConfirmResolution")]
        public async Task<IActionResult> ConfirmResolution([FromBody] ResolutionConfirmationDto confirmationDto)
        {
            var resolution = await _context.PEIssueResolutions.FindAsync(confirmationDto.ResolutionId);
            if (resolution == null)
            {
                return NotFound("Resolution not found");
            }

            var issue = await _context.PEIssues.FindAsync(resolution.IssueId);
            if (issue == null)
            {
                return NotFound("Issue not found");
            }

            var pe = await _context.PlannedEvents.FindAsync(resolution.PlannedEventId);

            if (confirmationDto.IsConfirmed)
            {
                // Update resolution status
                resolution.IsConfirmed = true;
                resolution.ConfirmedDate = DateTime.Now;
                _context.Update(resolution);

                // Mark issue as resolved
                issue.IsResolved = true;
                _context.Update(issue);

                // Find and mark the original issue as resolved if this is a reply
                if (issue.OriginalIssueId.HasValue)
                {
                    var originalIssue = await _context.PEIssues.FindAsync(issue.OriginalIssueId);
                    if (originalIssue != null && !originalIssue.IsResolved)
                    {
                        originalIssue.IsResolved = true;
                        _context.Update(originalIssue);
                    }
                }

                // Find the resolution request message and hide it from inbox
                var resolutionRequestMessage = await _context.PEIssues
                    .FirstOrDefaultAsync(i => i.IsResolutionRequest && i.OriginalIssueId == issue.Id);

                if (resolutionRequestMessage != null)
                {
                    resolutionRequestMessage.IsHiddenFromInbox = true;
                    _context.Update(resolutionRequestMessage);
                }

                // Update planned event - ONLY if no other active issues remain
                if (pe != null)
                {
                    // Check if any unresolved root issues remain
                    var hasOtherActiveIssues = await _context.PEIssues
                        .AnyAsync(i => i.PlannedEventId == pe.Id &&
                                  !i.IsResolved &&
                                  i.OriginalIssueId == null);  // Only consider root issues

                    if (!hasOtherActiveIssues)
                    {
                        pe.IsHold = false;  // Set IsHold to false
                        _context.Update(pe);
                    }
                }
            }
            else
            {
                // If rejected, delete the resolution request and create a notification
                _context.Remove(resolution);

                // Notify the user who attempted to fix the issue
                var notification = new PEIssue
                {
                    PlannedEventId = resolution.PlannedEventId,
                    PETaskId = issue.PETaskId,
                    SenderId = issue.SenderId, // Original reporter
                    ReceiverId = issue.ReceiverId, // User who tried to fix it
                    IssueText = $"RESOLUTION REJECTED: The fix was not accepted. Please try again.",
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    IsReply = true,
                    OriginalIssueId = issue.Id
                };

                _context.PEIssues.Add(notification);
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/IssuesApi/Suggestions/5
        [HttpGet("Suggestions/{taskId}")]
        public async Task<ActionResult<object>> GetIssueSuggestions(int taskId)
        {
            try
            {
                // First, get suggestions from SubTaskList table (more reliable)
                var subTaskSuggestions = await _context.SubTaskLists
                    .Where(s => s.PETaskListId == taskId)
                    .OrderByDescending(s => s.Frequency)
                    .ThenByDescending(s => s.LastReported)
                    .Take(10)
                    .Select(s => new
                    {
                        IssueText = s.SubTaskName,
                        Frequency = s.Frequency,
                        LastReported = s.LastReported,
                        Source = "common"
                    })
                    .ToListAsync();

                // Then, get previous issues for this task
                var recentIssueSuggestions = await _context.PEIssues
                    .Where(i => i.PETaskId == taskId && !i.IsReply && !i.IsResolutionRequest)
                    .OrderByDescending(i => i.CreatedAt)
                    .Take(5)
                    .Select(i => new
                    {
                        IssueText = i.IssueText,
                        ReportedBy = _context.Users
                            .Where(u => u.Id == i.SenderId)
                            .Select(u => u.Name)
                            .FirstOrDefault() ?? "Unknown",
                        CreatedAt = i.CreatedAt,
                        IsResolved = i.IsResolved,
                        Source = "recent"
                    })
                    .ToListAsync();

                // Combine and return both types of suggestions
                var combinedSuggestions = new
                {
                    CommonIssues = subTaskSuggestions,
                    RecentIssues = recentIssueSuggestions
                };

                return Ok(combinedSuggestions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching issue suggestions for task ID {taskId}");
                return StatusCode(500, "An error occurred while fetching suggestions");
            }
        }

        private async Task<int> GetCurrentUserIdAsync()
        {
            var serviceId = User.Identity?.Name;
            if (string.IsNullOrEmpty(serviceId))
            {
                return 0;
            }

            // Extract the substring before the query (first 6 characters)
            var serviceIdShort = serviceId.Length > 6 ? serviceId.Substring(0, 6) : serviceId;

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.ServiceId == serviceIdShort);

            return user?.Id ?? 0;
        }
    }

    public class IssueCreateDto
    {
        public int PlannedEventId { get; set; }
        public int PETaskId { get; set; }
        public int ReceiverId { get; set; }
        public string IssueText { get; set; }
        public IFormFile Attachment { get; set; }
    }

    public class IssueReplyDto
    {
        public int IssueId { get; set; }
        public string ReplyText { get; set; }
        public IFormFile Attachment { get; set; }
    }

    public class IssueResolutionDto
    {
        public int IssueId { get; set; }
        public int PlannedEventId { get; set; }
        public string ResolutionDetails { get; set; }
    }

    public class ResolutionConfirmationDto
    {
        public int ResolutionId { get; set; }
        public bool IsConfirmed { get; set; }
    }
}