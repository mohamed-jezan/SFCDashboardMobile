using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SFCDashboardMobile.Data;
using SFCDashboardMobile.Models;

namespace SFCDashboardMob.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WorkGroupsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly int _pageSize = 10;

        public WorkGroupsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /WorkGroups?page=1&searchTerm=abc
        [HttpGet]
        public async Task<IActionResult> Get(int? page, string? searchTerm)
        {
            var query = _context.WorkGroups.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
                query = query.Where(w => w.Name.Contains(searchTerm));

            var pageNumber = page ?? 1;
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)_pageSize);

            var items = await query
                .OrderBy(w => w.Name)
                .Skip((pageNumber - 1) * _pageSize)
                .Take(_pageSize)
                .ToListAsync();

            return Ok(new
            {
                CurrentPage = pageNumber,
                TotalPages = totalPages,
                TotalItems = totalItems,
                Items = items
            });
        }

        // GET: /WorkGroups/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var workGroup = await _context.WorkGroups.FindAsync(id);

            if (workGroup == null)
                return NotFound();

            return Ok(workGroup);
        }

        // POST: /WorkGroups
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] WorkGroup workGroup)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _context.WorkGroups.Add(workGroup);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = workGroup.Id }, workGroup);
        }

        // PUT: /WorkGroups/5
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Put(int id, [FromBody] WorkGroup workGroup)
        {
            if (id != workGroup.Id)
                return BadRequest("ID mismatch");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var exists = await _context.WorkGroups.AnyAsync(w => w.Id == id);
            if (!exists)
                return NotFound();

            _context.Entry(workGroup).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.WorkGroups.AnyAsync(w => w.Id == id))
                    return NotFound();
                else
                    throw;
            }

            return NoContent();
        }

        // DELETE: /WorkGroups/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var workGroup = await _context.WorkGroups.FindAsync(id);
            if (workGroup == null)
                return NotFound();

            _context.WorkGroups.Remove(workGroup);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: /WorkGroups/ImportFromBackend
        [HttpPost("ImportFromBackend")]
        public async Task<IActionResult> ImportFromBackend()
        {
            string filePath = @"wwwroot\assets\WORK_GROUPS.xlsx";

            if (!System.IO.File.Exists(filePath))
                return NotFound("Excel file not found.");

            try
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RowsUsed().Skip(1); // skip header

                var workGroups = new List<WorkGroup>();
                foreach (var row in rows)
                {
                    var name = row.Cell(1).GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(name))
                        workGroups.Add(new WorkGroup { Name = name });
                }

                await _context.WorkGroups.AddRangeAsync(workGroups);
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Excel data imported successfully!", ImportedCount = workGroups.Count });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = "Error during import", Details = ex.Message });
            }
        }
    }
}
