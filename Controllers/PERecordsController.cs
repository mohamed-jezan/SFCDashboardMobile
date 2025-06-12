using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SFCDashboardMobile.Data;
using SFCDashboardMobile.Models;
using SFCDashboardMobile.Services;
using SFCDashboardMobile.Swagger;

namespace SFCDashboardMobile.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PERecordsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<PERecordsApiController> _logger;

        public PERecordsApiController(
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,
            ILogger<PERecordsApiController> logger)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        [HttpGet("records")]
        public async Task<IActionResult> GetAllPERecords()
        {
            var records = await _context.PERecords.ToListAsync();
            return Ok(records);
        }

        [HttpPost("import-excel")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportExcel(IFormFile file)
        {
            if (file == null || file.Length <= 0)
                return BadRequest("Please select an Excel file to upload.");

            if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Please upload a valid .xlsx file.");

            var fileName = Path.GetFileName(file.FileName);
            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "temp");

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            var fullPath = Path.Combine(filePath, fileName);

            try
            {
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var peRecords = new List<PERecord>();

                using (var workbook = new XLWorkbook(fullPath))
                {
                    var worksheet = workbook.Worksheet(1);
                    var rows = worksheet.RowsUsed();

                    foreach (var row in rows.Skip(1))
                    {
                        var peNumber = row.Cell(7).GetValue<string>();
                        if (string.IsNullOrWhiteSpace(peNumber))
                            continue;

                        var peRecord = new PERecord
                        {
                            PROVINCE = GetCellValueSafely<string>(row, 1),
                            REGION = GetCellValueSafely<string>(row, 2),
                            RTOM = GetCellValueSafely<string>(row, 3),
                            RTOM_DESCRIPTION = GetCellValueSafely<string>(row, 4),
                            JOB_REFERENCE = GetCellValueSafely<string>(row, 5),
                            CONTRACTOR_NAME = GetCellValueSafely<string>(row, 6),
                            PE_NUMBER = peNumber,
                            PE_ACTIVITY = GetCellValueSafely<string>(row, 8),
                            PE_NATURE = GetCellValueSafely<string>(row, 9),
                            PE_TITLE = GetCellValueSafely<string>(row, 10),
                            PE_OBJECTIVE = GetCellValueSafely<string>(row, 11),
                            PE_AREA = GetCellValueSafely<string>(row, 12),
                            SO_NUMBER = GetCellValueSafely<string>(row, 13),
                            TASK_SEQ = GetCellValueSafely<int?>(row, 14),
                            TASK_NAME = GetCellValueSafely<string>(row, 15),
                            TASK_WG = GetCellValueSafely<string>(row, 16),
                            WO_ACTUAL_START_DATE = GetCellValueSafely<string>(row, 17),
                            REQUEST_REFERENCE_NO = GetCellValueSafely<string>(row, 18),
                            SO_ID = GetCellValueSafely<string>(row, 19),
                            REGION_1 = GetCellValueSafely<string>(row, 20),
                            PROVINCE_1 = GetCellValueSafely<string>(row, 21),
                            RTOM_1 = GetCellValueSafely<string>(row, 22),
                            LEA = GetCellValueSafely<string>(row, 23),
                            CCT_ID = GetCellValueSafely<string>(row, 24),
                            SERVICE_CATEGORY = GetCellValueSafely<string>(row, 25),
                            SERVICE_TYPE = GetCellValueSafely<string>(row, 26),
                            SO_CREATE_DATE = GetCellValueSafely<DateTime?>(row, 27),
                            ORDER_TYPE = GetCellValueSafely<string>(row, 28),
                            CRM_ORDER = GetCellValueSafely<string>(row, 29),
                            WO_ID = GetCellValueSafely<string>(row, 30),
                            PENDING_TASK_NAME = GetCellValueSafely<string>(row, 31),
                            PENDING_WG = GetCellValueSafely<string>(row, 32),
                            WO_STATUS = GetCellValueSafely<string>(row, 33),
                            WO_START_DATE = GetCellValueSafely<DateTime?>(row, 34),
                            SERVICE_SPEED = GetCellValueSafely<string>(row, 35),
                            SERVICE_REQUIRED_DATE = GetCellValueSafely<DateTime?>(row, 36),
                            FIBER_PE_NO = GetCellValueSafely<string>(row, 37),
                            FIBER_SO_ID = GetCellValueSafely<string>(row, 38),
                            PRODUCT_SO_ID = GetCellValueSafely<string>(row, 39),
                            FIBER_PE_TASK_NAME = GetCellValueSafely<string>(row, 40),
                            FIBER_PE_TASK_WG = GetCellValueSafely<string>(row, 41),
                            PE_WO_COMMENTS = GetCellValueSafely<string>(row, 42),
                            CUSTOMER = GetCellValueSafely<string>(row, 43),
                            CUS_TYPE = GetCellValueSafely<string>(row, 44),
                            ACCOUNT_MANAGER = GetCellValueSafely<string>(row, 45),
                            SECTION_HANDLED_BY = GetCellValueSafely<string>(row, 46),
                            LOCATION_A_ADDRESS = GetCellValueSafely<string>(row, 47),
                            LOCATION_B_ADDRESS = GetCellValueSafely<string>(row, 48),
                            NTU_TYPE = GetCellValueSafely<string>(row, 49),
                            ACCESS_MEDIUM = GetCellValueSafely<string>(row, 50),
                            ACCESS_MEDIUM_A_END = GetCellValueSafely<string>(row, 51),
                            ACCESS_MEDIUM_B_END = GetCellValueSafely<string>(row, 52),
                            WO_COMMENTS = GetCellValueSafely<string>(row, 53)
                        };

                        peRecords.Add(peRecord);
                    }
                }

                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);

                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    _context.PERecords.RemoveRange(_context.PERecords);
                    await _context.SaveChangesAsync();

                    await _context.PERecords.AddRangeAsync(peRecords);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();
                }

                var templateCount = await _context.PETaskLists.CountAsync();
                if (templateCount == 0)
                    return Ok("Records imported, but no task templates found. Sync skipped.");

                var syncService = HttpContext.RequestServices.GetRequiredService<PERecordSyncService>();
                await syncService.SyncPERecordsAsync();

                return Ok(new
                {
                    message = $"Imported {peRecords.Count} records and synced successfully.",
                    peCount = await _context.PERecords.CountAsync(),
                    plannedEventCount = await _context.PlannedEvents.CountAsync(),
                    peTaskCount = await _context.PETasks.CountAsync()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing Excel");
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        private T GetCellValueSafely<T>(IXLRow row, int columnIndex)
        {
            try
            {
                var cell = row.Cell(columnIndex);
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)(cell.IsEmpty() ? string.Empty : cell.GetValue<string>());
                }
                return cell.IsEmpty() ? default : cell.GetValue<T>();
            }
            catch
            {
                return default;
            }
        }
    }
}
