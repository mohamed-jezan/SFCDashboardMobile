using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SFCDashboardMobile.Data;
using SFCDashboardMobile.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SFCDashboardMobile.Services
{
    public class PERecordSyncService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<PERecordSyncService> _logger;
        private readonly TimeSpan _syncInterval = TimeSpan.FromHours(24);

        public PERecordSyncService(
            IServiceProvider services,
            ILogger<PERecordSyncService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PE Record Sync Service running");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SyncPERecordsAsync();
                    _logger.LogInformation("Sync completed successfully. Next sync at {nextRun}",
                        DateTime.Now.Add(_syncInterval));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during synchronization");
                }

                await Task.Delay(_syncInterval, stoppingToken);
            }
        }

        public async Task<SyncResult> SyncPERecordsAsync()
        {
            var result = new SyncResult();
            _logger.LogInformation("Starting PE records synchronization");

            using (var scope = _services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Load all required data in one roundtrip
                var sourceRecords = await dbContext.PERecords.AsNoTracking().ToListAsync();
                var existingEvents = await dbContext.PlannedEvents.AsNoTracking().ToListAsync();
                var existingTasks = await dbContext.PETasks.AsNoTracking().ToListAsync();
                var taskTemplates = await dbContext.PETaskLists.AsNoTracking().ToListAsync();

                if (sourceRecords.Count == 0 || taskTemplates.Count == 0)
                {
                    _logger.LogWarning("No source records or task templates found");
                    return result;
                }

                // Process records in batches
                foreach (var batch in sourceRecords
                    .Where(r => !string.IsNullOrEmpty(r.PE_NUMBER))
                    .GroupBy(r => r.PE_NUMBER)
                    .Chunk(100))
                {
                    foreach (var group in batch)
                    {
                        try
                        {
                            var record = group.OrderByDescending(r => r.SO_CREATE_DATE ?? DateTime.MinValue)
                                             .First();

                            await ProcessPeRecord(dbContext, record, existingEvents,
                                existingTasks, taskTemplates, result);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing PE: {peNumber}", group.Key);
                            result.Errors++;
                        }
                    }

                    // Save changes after each batch
                    await dbContext.SaveChangesAsync();
                }

                // Update task phases in bulk
                await UpdateTaskPhasesEfficiently(dbContext);
            }

            _logger.LogInformation("Sync completed: {new} new, {updated} updated, {errors} errors",
                result.NewEvents, result.UpdatedEvents, result.Errors);

            return result;
        }

        private async Task ProcessPeRecord(
            ApplicationDbContext dbContext,
            PERecord record,
            List<PlannedEvent> existingEvents,
            List<PETask> existingTasks,
            List<PETaskList> taskTemplates,
            SyncResult result)
        {
            var peNumber = record.PE_NUMBER;
            var existingEvent = existingEvents.FirstOrDefault(e => e.PeNumber == peNumber);

            if (existingEvent != null)
            {
                // Update existing event
                UpdatePlannedEvent(existingEvent, record);
                dbContext.PlannedEvents.Update(existingEvent);
                result.UpdatedEvents++;

                // Create or update tasks
                if (!existingTasks.Any(t => t.PENumber == peNumber))
                {
                    await CreatePETasksForEvent(dbContext, existingEvent, record, taskTemplates);
                }
                else
                {
                    await UpdatePETasksForEvent(dbContext, existingEvent, record, taskTemplates);
                }
            }
            else
            {
                // Create new event
                var newEvent = CreatePlannedEventFromRecord(record);
                dbContext.PlannedEvents.Add(newEvent);
                result.NewEvents++;

                // Create tasks for new event
                await CreatePETasksForEvent(dbContext, newEvent, record, taskTemplates);
            }
        }

        private void UpdatePlannedEvent(PlannedEvent existingEvent, PERecord record)
        {
            // Simplified property mapping - expand as needed
            existingEvent.PeTitle = record.PE_TITLE ?? existingEvent.PeTitle;
            existingEvent.TaskName = record.TASK_NAME ?? existingEvent.TaskName;
            existingEvent.TaskWg = record.TASK_WG ?? existingEvent.TaskWg;
            existingEvent.WoStatus = record.WO_STATUS ?? existingEvent.WoStatus;
            existingEvent.PEStatus = "ongoing";

        }

        private PlannedEvent CreatePlannedEventFromRecord(PERecord record)
        {
            return new PlannedEvent
            {
                PeNumber = record.PE_NUMBER,
                PeTitle = record.PE_TITLE,
                TaskName = record.TASK_NAME,
                TaskWg = record.TASK_WG,
                PEStatus = "ongoing",
                PECreatedDate = DateTime.UtcNow,

                // Add all other properties from record
            };
        }

        private async Task CreatePETasksForEvent(
            ApplicationDbContext dbContext,
            PlannedEvent plannedEvent,
            PERecord record,
            List<PETaskList> taskTemplates)
        {
            var tasks = CreateTasksFromTemplates(plannedEvent, record, taskTemplates);
            await dbContext.PETasks.AddRangeAsync(tasks);
        }

        private async Task UpdatePETasksForEvent(
            ApplicationDbContext dbContext,
            PlannedEvent plannedEvent,
            PERecord record,
            List<PETaskList> taskTemplates)
        {
            var existingTasks = await dbContext.PETasks
                .Where(t => t.PENumber == plannedEvent.PeNumber)
                .ToListAsync();

            var currentTasks = CreateTasksFromTemplates(plannedEvent, record, taskTemplates);

            foreach (var task in currentTasks)
            {
                var existing = existingTasks.FirstOrDefault(t =>
                    t.TaskSeq == task.TaskSeq && t.Task == task.Task);

                if (existing != null)
                {
                    // Update existing task
                    existing.TaskStatus = task.TaskStatus;
                    existing.TaskWorkGroup = task.TaskWorkGroup;
                    dbContext.PETasks.Update(existing);
                }
                else
                {
                    // Add new task
                    await dbContext.PETasks.AddAsync(task);
                }
            }
        }

        private List<PETask> CreateTasksFromTemplates(
            PlannedEvent plannedEvent,
            PERecord record,
            List<PETaskList> taskTemplates)
        {
            var tasks = new List<PETask>();
            var currentDate = DateTime.UtcNow;

            foreach (var template in taskTemplates
                .Where(t => !string.IsNullOrEmpty(t.OLA_Parameters))
                .OrderBy(t => t.TaskSeq))
            {
                var status = DetermineTaskStatus(template, record);
                var workGroup = GetTaskWorkGroup(template, record);

                tasks.Add(new PETask
                {
                    PENumber = plannedEvent.PeNumber,
                    TaskSeq = template.TaskSeq,
                    Task = template.Name,
                    OLA = template.OLA_Parameters,
                    TaskStatus = status,
                    TaskWorkGroup = workGroup,
                    TaskCreatedDate = currentDate,
                    TaskCompleteDate = currentDate.AddDays(GetOlaDays(template)),
                    ActualTaskCreatedDate = status == "ONGOING" ? currentDate : null,
                    ACtualTaskCompleteDate = status == "COMPLETED" ? currentDate : null
                });
            }

            return tasks;
        }

        private string DetermineTaskStatus(PETaskList template, PERecord record)
        {
            if (template.Name == record.TASK_NAME) return "ONGOING";
            if (template.TaskSeq < (record.TASK_SEQ ?? 0)) return "COMPLETED";
            return "WAITING";
        }

        private string GetTaskWorkGroup(PETaskList template, PERecord record)
        {
            return template.Name == record.TASK_NAME && !string.IsNullOrEmpty(record.TASK_WG)
                ? record.TASK_WG
                : "NULL";
        }

        private int GetOlaDays(PETaskList template)
        {
            return int.TryParse(template.OLA_Parameters, out var days) ? days : 0;
        }

        private async Task UpdateTaskPhasesEfficiently(ApplicationDbContext dbContext)
        {
            var sql = @"UPDATE t SET 
                t.TaskStatus = CASE
                    WHEN t.Task = e.TASK_NAME THEN 'ONGOING'
                    WHEN t.TaskSeq < e.TASK_SEQ THEN 'COMPLETED'
                    ELSE 'WAITING'
                END,
                t.TaskWorkGroup = CASE
                    WHEN t.Task = e.TASK_NAME THEN e.TASK_WG
                    ELSE t.TaskWorkGroup
                END,
                t.ActualTaskCreatedDate = CASE
                    WHEN t.Task = e.TASK_NAME AND t.ActualTaskCreatedDate IS NULL THEN GETUTCDATE()
                    ELSE t.ActualTaskCreatedDate
                END,
                t.ACtualTaskCompleteDate = CASE
                    WHEN t.TaskSeq < e.TASK_SEQ AND t.ACtualTaskCompleteDate IS NULL THEN GETUTCDATE()
                    ELSE t.ACtualTaskCompleteDate
                END
                FROM PETasks t
                INNER JOIN PlannedEvents e ON t.PENumber = e.PeNumber";

            await dbContext.Database.ExecuteSqlRawAsync(sql);
        }
    }

    public class SyncResult
    {
        public int NewEvents { get; set; }
        public int UpdatedEvents { get; set; }
        public int Errors { get; set; }
    }
}