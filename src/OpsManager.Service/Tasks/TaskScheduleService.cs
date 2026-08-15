using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Tasks.DTOs;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Tasks;

public sealed record SchedulerOptions(int GenerationHorizonDays = 30);

public interface ITaskOccurrenceGeneratorService
{
    Task<OccurrenceGenerationResult> GenerateAsync(Guid scheduleId, DateOnly? throughDate = null, CancellationToken cancellationToken = default);
    Task<int> GenerateAllAsync(CancellationToken cancellationToken = default);
}

public interface ITaskScheduleService
{
    Task<PagedResponse<TaskScheduleDto>> ListAsync(PageQuery page, CancellationToken cancellationToken = default);
    Task<TaskScheduleDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskScheduleDto> CreateAsync(SaveTaskScheduleRequest request, CancellationToken cancellationToken = default);
    Task<TaskScheduleDto> UpdateAsync(Guid id, SaveTaskScheduleRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}

public static class TaskOccurrenceCalculator
{
    public static IReadOnlyList<DateOnly> Calculate(
        TaskSchedule schedule,
        DateOnly throughDate,
        IReadOnlyCollection<DateOnly>? specificDates = null)
    {
        DateOnly end = schedule.RecurrenceEndDate.HasValue && schedule.RecurrenceEndDate.Value < throughDate
            ? schedule.RecurrenceEndDate.Value
            : throughDate;
        if (end < schedule.RecurrenceStartDate)
        {
            return [];
        }

        return schedule.RecurrenceType switch
        {
            RecurrenceType.Daily => Daily(schedule, end),
            RecurrenceType.Weekly => Weekly(schedule, end),
            RecurrenceType.Monthly => Monthly(schedule, end),
            RecurrenceType.SpecificDates => SpecificDatesRange(schedule, end, specificDates ?? []),
            _ => throw new NotSupportedException($"Recurrence type '{schedule.RecurrenceType}' is not supported."),
        };
    }

    private static List<DateOnly> Daily(TaskSchedule schedule, DateOnly end)
    {
        List<DateOnly> dates = [];
        for (DateOnly date = schedule.RecurrenceStartDate; date <= end; date = date.AddDays(1))
        {
            dates.Add(date);
        }

        return dates;
    }

    private static List<DateOnly> Weekly(TaskSchedule schedule, DateOnly end)
    {
        HashSet<Weekday> weekdays = schedule.Weekdays.ToHashSet();
        List<DateOnly> dates = [];
        for (DateOnly date = schedule.RecurrenceStartDate; date <= end; date = date.AddDays(1))
        {
            if (weekdays.Contains((Weekday)date.DayOfWeek))
            {
                dates.Add(date);
            }
        }

        return dates;
    }

    private static List<DateOnly> Monthly(TaskSchedule schedule, DateOnly end)
    {
        SortedSet<DateOnly> dates = [];
        DateOnly cursor = new(schedule.RecurrenceStartDate.Year, schedule.RecurrenceStartDate.Month, 1);
        while (cursor <= end)
        {
            int daysInMonth = DateTime.DaysInMonth(cursor.Year, cursor.Month);
            foreach (int day in schedule.MonthDays)
            {
                if (day <= daysInMonth)
                {
                    DateOnly candidate = new(cursor.Year, cursor.Month, day);
                    if (candidate >= schedule.RecurrenceStartDate && candidate <= end)
                    {
                        dates.Add(candidate);
                    }
                }
            }

            if (schedule.IncludeLastDayOfMonth)
            {
                DateOnly lastDay = new(cursor.Year, cursor.Month, daysInMonth);
                if (lastDay >= schedule.RecurrenceStartDate && lastDay <= end)
                {
                    dates.Add(lastDay);
                }
            }

            cursor = cursor.AddMonths(1);
        }

        return [.. dates];
    }

    private static List<DateOnly> SpecificDatesRange(
        TaskSchedule schedule,
        DateOnly end,
        IReadOnlyCollection<DateOnly> specificDates)
    {
        return specificDates
            .Where(date => date >= schedule.RecurrenceStartDate && date <= end)
            .Distinct()
            .Order()
            .ToList();
    }
}

public sealed class TaskScheduleService(
    IUnitOfWork unitOfWork,
    ICurrentUserContext currentUser,
    ISubscriptionAccessService subscriptionAccess,
    IAuditService auditService,
    IClock clock,
    IRequestValidator<SaveTaskScheduleRequest> validator,
    ITaskAssigneeResolver assigneeResolver) : ITaskScheduleService
{
    public async Task<PagedResponse<TaskScheduleDto>> ListAsync(PageQuery page, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireSupervisorOrManager(currentUser);
        PagedResult<TaskSchedule> result = await unitOfWork.Repository<TaskSchedule>()
            .ListAsync(schedule => schedule.OrganizationId == organizationId, page.ToDomain(), cancellationToken);
        List<TaskScheduleDto> items = [];
        foreach (TaskSchedule schedule in result.Items)
        {
            items.Add(await MapAsync(schedule, cancellationToken));
        }

        return new PagedResponse<TaskScheduleDto>(items, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<TaskScheduleDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _ = AuthorizationGuard.RequireSupervisorOrManager(currentUser);
        return await MapAsync(await GetEntityAsync(id, cancellationToken), cancellationToken);
    }

    public async Task<TaskScheduleDto> CreateAsync(
        SaveTaskScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(
            organizationId,
            OpsManager.Domain.Constants.SubscriptionFeatureKeys.Tasks,
            cancellationToken);
        validator.ValidateAndThrow(request);
        await ValidateScopeAsync(request, cancellationToken);
        TaskSchedule schedule = CreateEntity(organizationId, request, currentUser.UserId!.Value);
        await unitOfWork.Repository<TaskSchedule>().AddAsync(schedule, cancellationToken);
        await ReplaceAssigneesAsync(schedule, request.Assignment.UserIds, cancellationToken);
        await ReplaceSpecificDatesAsync(schedule, request.SpecificDates, cancellationToken);
        await auditService.RecordTenantAsync(
            organizationId,
            "task-schedule.created",
            nameof(TaskSchedule),
            schedule.Id,
            cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapAsync(schedule, cancellationToken);
    }

    public async Task<TaskScheduleDto> UpdateAsync(
        Guid id,
        SaveTaskScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        validator.ValidateAndThrow(request);
        TaskSchedule schedule = await GetEntityAsync(id, cancellationToken);
        await ValidateScopeAsync(request, cancellationToken);
        schedule.Update(
            request.TaskTemplateId,
            request.BranchId,
            request.DepartmentId,
            request.Assignment.Mode,
            request.RecurrenceType,
            request.Weekdays,
            request.MonthDays,
            request.IncludeLastDayOfMonth,
            request.RecurrenceStartDate,
            request.RecurrenceEndDate,
            request.ExecutionStartTime,
            request.ExecutionDueTime,
            request.ExecutionDueDayOffset);
        if (request.IsActive)
        {
            schedule.Activate();
        }
        else
        {
            schedule.Deactivate();
        }
        unitOfWork.Repository<TaskSchedule>().Update(schedule);
        await ReplaceAssigneesAsync(schedule, request.Assignment.UserIds, cancellationToken);
        await ReplaceSpecificDatesAsync(schedule, request.SpecificDates, cancellationToken);

        // Update existing unstarted (NotStarted) task instances generated by this schedule
        PagedResult<OperationalTask> pendingTasks = await unitOfWork.Repository<OperationalTask>().ListAsync(
            task => task.TaskScheduleId == schedule.Id &&
                task.Status == OperationalTaskStatus.NotStarted,
            new PageRequest(1, PageRequest.MaximumPageSize),
            cancellationToken);

        TaskTemplate template = await unitOfWork.Repository<TaskTemplate>().GetByIdAsync(schedule.TaskTemplateId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(TaskTemplate));
        Branch branch = await unitOfWork.Repository<Branch>().GetByIdAsync(schedule.BranchId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Branch));
        TimeZoneInfo timezone = TimeZoneInfo.FindSystemTimeZoneById(branch.Timezone);

        foreach (OperationalTask task in pendingTasks.Items)
        {
            DateTimeOffset newStart = TaskOccurrenceGeneratorService.ToUtc(task.OccurrenceDate, schedule.ExecutionStartTime, timezone);
            DateTimeOffset newDue = TaskOccurrenceGeneratorService.ToUtc(task.OccurrenceDate.AddDays(schedule.ExecutionDueDayOffset), schedule.ExecutionDueTime, timezone);
            task.Reschedule(task.OccurrenceDate, newStart, newDue, false);
            task.UpdateDetails(template.Title, template.Description, template.DefaultPriority, template.RequiresApproval);
            unitOfWork.Repository<OperationalTask>().Update(task);
        }

        await auditService.RecordTenantAsync(
            organizationId,
            "task-schedule.updated",
            nameof(TaskSchedule),
            schedule.Id,
            cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapAsync(schedule, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        TaskSchedule schedule = await GetEntityAsync(id, cancellationToken);
        schedule.Deactivate();
        unitOfWork.Repository<TaskSchedule>().Update(schedule);

        // Cancel / clear all pending uncompleted task instances generated by this schedule
        PagedResult<OperationalTask> pendingTasks = await unitOfWork.Repository<OperationalTask>().ListAsync(
            task => task.TaskScheduleId == schedule.Id &&
                (task.Status == OperationalTaskStatus.NotStarted ||
                 task.Status == OperationalTaskStatus.InProgress ||
                 task.Status == OperationalTaskStatus.Blocked),
            new PageRequest(1, PageRequest.MaximumPageSize),
            cancellationToken);

        DateTimeOffset now = clock.UtcNow;
        Guid actorId = currentUser.UserId!.Value;
        foreach (OperationalTask task in pendingTasks.Items)
        {
            task.Cancel(actorId, now, "Task schedule was deleted or deactivated.");
            unitOfWork.Repository<OperationalTask>().Update(task);
        }

        await auditService.RecordTenantAsync(
            organizationId,
            "task-schedule.deleted",
            nameof(TaskSchedule),
            id,
            cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        TaskSchedule schedule = await GetEntityAsync(id, cancellationToken);
        if (isActive)
        {
            schedule.Activate();
        }
        else
        {
            schedule.Deactivate();
        }
        unitOfWork.Repository<TaskSchedule>().Update(schedule);
        await auditService.RecordTenantAsync(
            organizationId,
            isActive ? "task-schedule.activated" : "task-schedule.deactivated",
            nameof(TaskSchedule),
            id,
            cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<TaskSchedule> GetEntityAsync(Guid id, CancellationToken cancellationToken)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        TaskSchedule schedule = await unitOfWork.Repository<TaskSchedule>().GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(TaskSchedule));
        return schedule.OrganizationId == organizationId
            ? schedule
            : throw new EntityNotFoundException(nameof(TaskSchedule));
    }

    private async Task ValidateScopeAsync(SaveTaskScheduleRequest request, CancellationToken cancellationToken)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        TaskTemplate template = await unitOfWork.Repository<TaskTemplate>().GetByIdAsync(request.TaskTemplateId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(TaskTemplate));
        Department department = await unitOfWork.Repository<Department>().GetByIdAsync(request.DepartmentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Department));
        Branch branch = await unitOfWork.Repository<Branch>().GetByIdAsync(request.BranchId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Branch));
        if (template.OrganizationId != organizationId ||
            department.OrganizationId != organizationId ||
            branch.OrganizationId != organizationId)
        {
            throw new EntityNotFoundException("Schedule scope");
        }

        if (!template.IsActive || !department.IsActive || !branch.IsActive || department.BranchId != branch.Id)
        {
            throw new ConflictException("Schedule scope resources must be active and department must belong to branch.", "invalid_schedule_scope");
        }

        _ = await assigneeResolver.ResolveAsync(
            organizationId,
            branch.Id,
            department.Id,
            request.Assignment.Mode,
            request.Assignment.UserIds,
            cancellationToken);
    }

    private static TaskSchedule CreateEntity(Guid organizationId, SaveTaskScheduleRequest request, Guid createdBy)
    {
        TaskSchedule schedule = new(
            organizationId,
            request.TaskTemplateId,
            request.BranchId,
            request.DepartmentId,
            request.Assignment.Mode,
            request.RecurrenceType,
            request.RecurrenceStartDate,
            request.ExecutionStartTime,
            request.ExecutionDueTime,
            createdBy,
            request.Weekdays,
            request.MonthDays,
            request.IncludeLastDayOfMonth,
            request.RecurrenceEndDate,
            request.ExecutionDueDayOffset,
            request.IsActive);
        return schedule;
    }

    private async Task<TaskScheduleDto> MapAsync(
        TaskSchedule schedule,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> assigneeIds = await unitOfWork.Repository<TaskScheduleAssignee>().ProjectAsync(
            assignee => assignee.TaskScheduleId == schedule.Id,
            assignee => assignee.UserId,
            cancellationToken);
        IReadOnlyList<DateOnly> specificDates = await unitOfWork.Repository<TaskScheduleDate>().ProjectAsync(
            date => date.TaskScheduleId == schedule.Id,
            date => date.OccurrenceDate,
            cancellationToken);
        return new(
            schedule.Id,
            schedule.TaskTemplateId,
            schedule.BranchId,
            schedule.DepartmentId,
            schedule.AssignmentMode,
            assigneeIds.Order().ToArray(),
            schedule.RecurrenceType,
            schedule.Weekdays.ToArray(),
            schedule.MonthDays.ToArray(),
            schedule.IncludeLastDayOfMonth,
            specificDates.Order().ToArray(),
            schedule.RecurrenceStartDate,
            schedule.RecurrenceEndDate,
            schedule.ExecutionStartTime,
            schedule.ExecutionDueTime,
            schedule.ExecutionDueDayOffset,
            schedule.IsActive);
    }

    private async Task ReplaceAssigneesAsync(
        TaskSchedule schedule,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        PagedResult<TaskScheduleAssignee> existing = await unitOfWork.Repository<TaskScheduleAssignee>().ListAsync(
            assignee => assignee.TaskScheduleId == schedule.Id,
            new PageRequest(1, PageRequest.MaximumPageSize),
            cancellationToken);
        foreach (TaskScheduleAssignee assignee in existing.Items)
        {
            unitOfWork.Repository<TaskScheduleAssignee>().DeletePermanently(assignee);
        }

        if (schedule.AssignmentMode != TaskAssignmentMode.AllDepartmentMembers)
        {
            await unitOfWork.Repository<TaskScheduleAssignee>().AddRangeAsync(
                userIds.Select(userId => new TaskScheduleAssignee(schedule.OrganizationId, schedule.Id, userId)),
                cancellationToken);
        }
    }

    private async Task ReplaceSpecificDatesAsync(
        TaskSchedule schedule,
        IReadOnlyCollection<DateOnly> dates,
        CancellationToken cancellationToken)
    {
        PagedResult<TaskScheduleDate> existing = await unitOfWork.Repository<TaskScheduleDate>().ListAsync(
            date => date.TaskScheduleId == schedule.Id,
            new PageRequest(1, PageRequest.MaximumPageSize),
            cancellationToken);

       
        foreach (TaskScheduleDate date in existing.Items)
        {
            unitOfWork.Repository<TaskScheduleDate>().DeletePermanently(date);
        }

        if (schedule.RecurrenceType == RecurrenceType.SpecificDates)
        {
            await unitOfWork.Repository<TaskScheduleDate>().AddRangeAsync(
                dates.Distinct().Select(date => new TaskScheduleDate(schedule.OrganizationId, schedule.Id, date)),
                cancellationToken);
        }
    }
}

public sealed class TaskOccurrenceGeneratorService(
    IUnitOfWork unitOfWork,
    ICurrentUserContext currentUser,
    IAuthenticationTenantScope tenantScope,
    IClock clock,
    SchedulerOptions options,
    ITaskAssigneeResolver assigneeResolver,
    ITaskDistributionCreator distributionCreator) : ITaskOccurrenceGeneratorService
{
    public async Task<OccurrenceGenerationResult> GenerateAsync(
        Guid scheduleId,
        DateOnly? throughDate = null,
        CancellationToken cancellationToken = default)
    {
        TaskSchedule schedule = await unitOfWork.Repository<TaskSchedule>().GetByIdAsync(scheduleId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(TaskSchedule));
        if (currentUser.IsAuthenticated && currentUser.OrganizationId != schedule.OrganizationId)
        {
            throw new EntityNotFoundException(nameof(TaskSchedule));
        }

        if (!schedule.IsActive)
        {
            return new OccurrenceGenerationResult(schedule.Id, 0, throughDate ?? Today().AddDays(options.GenerationHorizonDays));
        }

        DateOnly horizon = throughDate ?? Today().AddDays(options.GenerationHorizonDays);
        TaskTemplate template = await unitOfWork.Repository<TaskTemplate>().GetByIdAsync(schedule.TaskTemplateId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(TaskTemplate));
        Branch branch = await unitOfWork.Repository<Branch>().GetByIdAsync(schedule.BranchId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Branch));
        TimeZoneInfo timezone = TimeZoneInfo.FindSystemTimeZoneById(branch.Timezone);
        PagedResult<TaskTemplateItem> templateItems = await unitOfWork.Repository<TaskTemplateItem>().ListAsync(
            item => item.TaskTemplateId == template.Id && item.IsActive,
            new PageRequest(1, PageRequest.MaximumPageSize),
            cancellationToken);
        IReadOnlyList<Guid> configuredAssignees = await unitOfWork.Repository<TaskScheduleAssignee>().ProjectAsync(
            assignee => assignee.TaskScheduleId == schedule.Id,
            assignee => assignee.UserId,
            cancellationToken);
        int created = 0;
        IReadOnlyList<DateOnly> specificDates = schedule.RecurrenceType == RecurrenceType.SpecificDates
            ? await unitOfWork.Repository<TaskScheduleDate>().ProjectAsync(
                date => date.TaskScheduleId == schedule.Id,
                date => date.OccurrenceDate,
                cancellationToken)
            : [];
        foreach (DateOnly date in TaskOccurrenceCalculator.Calculate(schedule, horizon, specificDates))
        {
            DateTimeOffset start = ToUtc(date, schedule.ExecutionStartTime, timezone);
            bool exists = await unitOfWork.Repository<TaskDistribution>().AnyAsync(
                distribution => distribution.TaskScheduleId == schedule.Id &&
                    distribution.OccurrenceDate == date &&
                    distribution.ScheduledStartAt == start,
                cancellationToken);
            if (exists)
            {
                continue;
            }

            DateTimeOffset due = ToUtc(date.AddDays(schedule.ExecutionDueDayOffset), schedule.ExecutionDueTime, timezone);
            IReadOnlyList<ResolvedTaskAssignee> assignees = await assigneeResolver.ResolveScheduledAsync(
                schedule.OrganizationId,
                schedule.BranchId,
                schedule.DepartmentId,
                schedule.AssignmentMode,
                configuredAssignees,
                cancellationToken);
            TaskDistributionResponse result = await distributionCreator.CreateAsync(
                new TaskDistributionCreation(
                    schedule.OrganizationId,
                    schedule.BranchId,
                    schedule.DepartmentId,
                    template.Id,
                    schedule.Id,
                    null,
                    schedule.AssignmentMode,
                    assignees,
                    template.Title,
                    template.Description,
                    date,
                    start,
                    due,
                    template.DefaultPriority,
                    template.RequiresApproval,
                    schedule.CreatedBy,
                    templateItems.Items.Select(item => new TaskCopyItemDefinition(
                        item.Id,
                        item.Title,
                        item.Description,
                        item.SortOrder,
                        item.IsRequired,
                        item.EvidenceMode)).ToArray()),
                cancellationToken);
            created += result.CreatedTaskCount;
        }
        return new OccurrenceGenerationResult(schedule.Id, created, horizon);
    }

    public async Task<int> GenerateAllAsync(CancellationToken cancellationToken = default)
    {
        using IDisposable bypass = tenantScope.BeginBypass();
        IReadOnlyList<(Guid Id, Guid OrganizationId)> schedules =
            await unitOfWork.Repository<TaskSchedule>().ProjectAsync(
                schedule => schedule.IsActive,
                schedule => new ValueTuple<Guid, Guid>(schedule.Id, schedule.OrganizationId),
                cancellationToken);
        int created = 0;
        foreach ((Guid scheduleId, Guid organizationId) in schedules)
        {
            using IDisposable tenant = tenantScope.Begin(organizationId);
            OccurrenceGenerationResult result = await GenerateAsync(scheduleId, null, cancellationToken);
            created += result.CreatedCount;
        }

        return created;
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

    public static DateTimeOffset ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo timezone)
    {
        DateTime local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (timezone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        if (timezone.IsAmbiguousTime(local))
        {
            TimeSpan offset = timezone.GetAmbiguousTimeOffsets(local).Max();
            return new DateTimeOffset(local, offset).ToUniversalTime();
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timezone), TimeSpan.Zero);
    }
}
