using System.Globalization;
using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Complaints.DTOs;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Complaints;

public interface IComplaintService
{
    Task<PagedResponse<ComplaintDto>> ListAsync(ComplaintQuery query, CancellationToken cancellationToken = default);
    Task<ComplaintDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ComplaintDto> CreateAsync(CreateComplaintRequest request, CancellationToken cancellationToken = default);
    Task<ComplaintDto> UpdateAsync(Guid id, UpdateComplaintRequest request, CancellationToken cancellationToken = default);
    Task<ComplaintDto> AssignAsync(Guid id, AssignComplaintRequest request, CancellationToken cancellationToken = default);
    Task<ComplaintDto> ChangeStatusAsync(
        Guid id,
        ComplaintStatus status,
        string? message = null,
        CancellationToken cancellationToken = default);
    Task<ComplaintMessageDto> AddMessageAsync(
        Guid id,
        ComplaintMessageRequest request,
        CancellationToken cancellationToken = default);
    Task<StoredFile> AddAttachmentAsync(
        Guid id,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}

public sealed class ComplaintService(
    IUnitOfWork unitOfWork,
    ICurrentUserContext currentUser,
    ISubscriptionAccessService subscriptionAccess,
    IAuditService auditService,
    INotificationService notifications,
    IFileStorageService fileStorage,
    IClock clock,
    IRequestValidator<CreateComplaintRequest> createValidator,
    IRequestValidator<UpdateComplaintRequest> updateValidator) : IComplaintService
{
    public async Task<PagedResponse<ComplaintDto>> ListAsync(
        ComplaintQuery query,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        Guid userId = currentUser.UserId!.Value;
        bool management = currentUser.OrganizationRole is OrganizationRole.Manager or OrganizationRole.Supervisor;
        IReadOnlyList<Guid>? departments = await GetManagementDepartmentsAsync(organizationId, cancellationToken);
        PagedResult<Complaint> result = await unitOfWork.Repository<Complaint>().ListAsync(
            complaint => complaint.OrganizationId == organizationId &&
                (management
                    ? departments == null ||
                        !complaint.TargetDepartmentId.HasValue ||
                        departments.Contains(complaint.TargetDepartmentId.Value) ||
                        complaint.AssignedTo == userId ||
                        complaint.SubmittedBy == userId
                    : complaint.SubmittedBy == userId && complaint.Visibility == ComplaintVisibility.Participants) &&
                (!query.Status.HasValue || complaint.Status == query.Status.Value) &&
                (!query.BranchId.HasValue || complaint.BranchId == query.BranchId.Value) &&
                (!query.DepartmentId.HasValue || complaint.TargetDepartmentId == query.DepartmentId.Value) &&
                (!query.AssigneeUserId.HasValue || complaint.AssignedTo == query.AssigneeUserId.Value),
            query.PageQuery.ToDomain(),
            cancellationToken);
        List<ComplaintDto> items = [];
        foreach (Complaint complaint in result.Items)
        {
            items.Add(await MapAsync(complaint, management, cancellationToken));
        }

        return new PagedResponse<ComplaintDto>(items, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<ComplaintDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        (Complaint complaint, bool management) = await GetAuthorizedAsync(id, cancellationToken);
        return await MapAsync(complaint, management, cancellationToken);
    }

    public async Task<ComplaintDto> CreateAsync(
        CreateComplaintRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        createValidator.ValidateAndThrow(request);
        Branch branch = await unitOfWork.Repository<Branch>().GetByIdAsync(request.BranchId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Branch));
        if (!branch.IsActive)
        {
            throw new ConflictException("The branch is inactive.", "inactive_branch");
        }

        if (request.TargetDepartmentId.HasValue)
        {
            Department department = await unitOfWork.Repository<Department>()
                .GetByIdAsync(request.TargetDepartmentId.Value, cancellationToken)
                ?? throw new EntityNotFoundException(nameof(Department));
            if (!department.IsActive || department.BranchId != branch.Id)
            {
                throw new ConflictException("The target department must be active in the selected branch.", "invalid_complaint_department");
            }
        }

        DateTimeOffset now = clock.UtcNow;
        string number = string.Create(
            CultureInfo.InvariantCulture,
            $"CMP-{now:yyyyMMdd}-{Guid.NewGuid():N}");
        Complaint complaint = new(
            organizationId,
            request.BranchId,
            number,
            currentUser.UserId!.Value,
            request.Title,
            request.Description,
            request.Visibility)
        {
            TargetDepartmentId = request.TargetDepartmentId,
        };
        await unitOfWork.Repository<Complaint>().AddAsync(complaint, cancellationToken);
        await AuditAsync(complaint, "complaint.created", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapAsync(complaint, currentUser.OrganizationRole != OrganizationRole.Employee, cancellationToken);
    }

    public async Task<ComplaintDto> UpdateAsync(
        Guid id,
        UpdateComplaintRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        updateValidator.ValidateAndThrow(request);
        (Complaint complaint, bool management) = await GetAuthorizedAsync(id, cancellationToken);
        if (!management && (complaint.SubmittedBy != currentUser.UserId || complaint.Status != ComplaintStatus.Submitted))
        {
            throw new ForbiddenAccessException("Only a submitted complaint can be edited by its submitter.");
        }

        if (complaint.Status is ComplaintStatus.Closed or ComplaintStatus.Rejected)
        {
            throw new ConflictException("Closed complaints are immutable.", "complaint_immutable");
        }

        complaint.Title = request.Title.Trim();
        complaint.Description = request.Description.Trim();
        complaint.Visibility = request.Visibility;
        unitOfWork.Repository<Complaint>().Update(complaint);
        await AuditAsync(complaint, "complaint.updated", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapAsync(complaint, management, cancellationToken);
    }

    public async Task<ComplaintDto> AssignAsync(
        Guid id,
        AssignComplaintRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireSupervisorOrManager(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        (Complaint complaint, _) = await GetAuthorizedAsync(id, cancellationToken);
        OrganizationMember? member = await unitOfWork.Repository<OrganizationMember>().FirstOrDefaultAsync(
            item => item.UserId == request.AssigneeUserId &&
                item.IsActive &&
                (item.Role == OrganizationRole.Manager || item.Role == OrganizationRole.Supervisor),
            cancellationToken);
        if (member is null)
        {
            throw new ConflictException("Complaint assignee must be an active Manager or Supervisor.", "invalid_complaint_assignee");
        }

        complaint.AssignedTo = request.AssigneeUserId;
        unitOfWork.Repository<Complaint>().Update(complaint);
        await AuditAsync(complaint, "complaint.assigned", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await TryNotifyAsync(complaint, request.AssigneeUserId, "Complaint assigned", cancellationToken);
        return await MapAsync(complaint, true, cancellationToken);
    }

    public async Task<ComplaintDto> ChangeStatusAsync(
        Guid id,
        ComplaintStatus status,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireSupervisorOrManager(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        (Complaint complaint, _) = await GetAuthorizedAsync(id, cancellationToken);
        EnsureTransition(complaint.Status, status);
        complaint.Status = status;
        if (status == ComplaintStatus.UnderReview)
        {
            complaint.ReviewedAt ??= clock.UtcNow;
        }

        if (status == ComplaintStatus.Closed)
        {
            complaint.ClosedAt = clock.UtcNow;
        }

        unitOfWork.Repository<Complaint>().Update(complaint);
        if (!string.IsNullOrWhiteSpace(message))
        {
            await AddMessageEntityAsync(complaint, new ComplaintMessageRequest(message, false), cancellationToken);
        }

        await AuditAsync(complaint, $"complaint.{status.ToString().ToLowerInvariant()}", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (status is ComplaintStatus.InProgress or ComplaintStatus.Resolved or ComplaintStatus.Closed)
        {
            await TryNotifyAsync(complaint, complaint.SubmittedBy, "Complaint updated", cancellationToken);
        }

        return await MapAsync(complaint, true, cancellationToken);
    }

    public async Task<ComplaintMessageDto> AddMessageAsync(
        Guid id,
        ComplaintMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        (Complaint complaint, bool management) = await GetAuthorizedAsync(id, cancellationToken);
        if (request.IsInternal && !management)
        {
            throw new ForbiddenAccessException("Internal complaint messages require management access.");
        }

        ComplaintMessage message = await AddMessageEntityAsync(complaint, request, cancellationToken);
        await AuditAsync(complaint, "complaint.message-added", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (management && !request.IsInternal)
        {
            await TryNotifyAsync(complaint, complaint.SubmittedBy, "Complaint response", cancellationToken);
        }

        return Map(message);
    }

    public async Task<StoredFile> AddAttachmentAsync(
        Guid id,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        _ = await GetAuthorizedAsync(id, cancellationToken);
        StoredFile file = await fileStorage.SaveAsync(content, fileName, contentType, cancellationToken);
        await unitOfWork.Repository<ComplaintAttachment>().AddAsync(
            new ComplaintAttachment
            {
                OrganizationId = organizationId,
                ComplaintId = id,
                UploadedBy = currentUser.UserId!.Value,
                FileUrl = file.Url,
                FileType = file.ContentType,
            },
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return file;
    }

    private async Task<(Complaint Complaint, bool Management)> GetAuthorizedAsync(Guid id, CancellationToken cancellationToken)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        Complaint complaint = await unitOfWork.Repository<Complaint>().GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Complaint));
        bool management = currentUser.OrganizationRole is OrganizationRole.Manager or OrganizationRole.Supervisor;
        if (!management)
        {
            if (complaint.SubmittedBy != currentUser.UserId || complaint.Visibility != ComplaintVisibility.Participants)
            {
                throw new EntityNotFoundException(nameof(Complaint));
            }

            return (complaint, false);
        }

        IReadOnlyList<Guid>? departments = await GetManagementDepartmentsAsync(organizationId, cancellationToken);
        if (departments is not null &&
            complaint.TargetDepartmentId.HasValue &&
            !departments.Contains(complaint.TargetDepartmentId.Value) &&
            complaint.AssignedTo != currentUser.UserId &&
            complaint.SubmittedBy != currentUser.UserId)
        {
            throw new EntityNotFoundException(nameof(Complaint));
        }

        return (complaint, true);
    }

    private async Task<IReadOnlyList<Guid>?> GetManagementDepartmentsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        if (currentUser.OrganizationRole == OrganizationRole.Manager)
        {
            return null;
        }

        if (currentUser.OrganizationRole != OrganizationRole.Supervisor)
        {
            return [];
        }

        Guid userId = currentUser.UserId!.Value;
        IReadOnlyList<Guid> direct = await unitOfWork.Repository<UserDepartment>().ProjectAsync(
            relation => relation.OrganizationId == organizationId && relation.UserId == userId && relation.LeftAt == null,
            relation => relation.DepartmentId,
            cancellationToken);
        IReadOnlyList<Guid> supervised = await unitOfWork.Repository<Department>().ProjectAsync(
            department => department.SupervisorUserId == userId && department.IsActive,
            department => department.Id,
            cancellationToken);
        return direct.Concat(supervised).Distinct().ToArray();
    }

    private async Task<ComplaintMessage> AddMessageEntityAsync(
        Complaint complaint,
        ComplaintMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 8000)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Message)] = ["Message is required and cannot exceed 8000 characters."],
            });
        }

        ComplaintMessage message = new()
        {
            OrganizationId = complaint.OrganizationId,
            ComplaintId = complaint.Id,
            SenderUserId = currentUser.UserId!.Value,
            MessageText = request.Message.Trim(),
            IsInternal = request.IsInternal,
        };
        await unitOfWork.Repository<ComplaintMessage>().AddAsync(message, cancellationToken);
        return message;
    }

    private Task EnsureWriteAsync(Guid organizationId, CancellationToken cancellationToken) =>
        subscriptionAccess.EnsureWriteAllowedAsync(
            organizationId,
            OpsManager.Domain.Constants.SubscriptionFeatureKeys.Complaints,
            cancellationToken);

    private Task AuditAsync(Complaint complaint, string action, CancellationToken cancellationToken) =>
        auditService.RecordTenantAsync(
            complaint.OrganizationId,
            action,
            nameof(Complaint),
            complaint.Id,
            cancellationToken: cancellationToken);

    private async Task TryNotifyAsync(
        Complaint complaint,
        Guid userId,
        string title,
        CancellationToken cancellationToken)
    {
        try
        {
            await notifications.CreateAsync(
                complaint.OrganizationId,
                userId,
                NotificationType.ComplaintUpdated,
                title,
                complaint.Title,
                relatedEntityType: nameof(Complaint),
                relatedEntityId: complaint.Id,
                cancellationToken: cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Best-effort post-commit notification.
        }
    }

    private async Task<ComplaintDto> MapAsync(
        Complaint complaint,
        bool includeInternal,
        CancellationToken cancellationToken)
    {
        PagedResult<ComplaintMessage> messages = await unitOfWork.Repository<ComplaintMessage>().ListAsync(
            message => message.ComplaintId == complaint.Id && (includeInternal || !message.IsInternal),
            new PageRequest(1, PageRequest.MaximumPageSize),
            cancellationToken);
        return new ComplaintDto(
            complaint.Id,
            complaint.ComplaintNumber,
            complaint.BranchId,
            complaint.SubmittedBy,
            complaint.TargetDepartmentId,
            complaint.AssignedTo,
            complaint.Title,
            complaint.Description,
            complaint.Status,
            complaint.Visibility,
            complaint.ReviewedAt,
            complaint.ClosedAt,
            messages.Items.Select(Map).ToArray());
    }

    private static ComplaintMessageDto Map(ComplaintMessage message) =>
        new(message.Id, message.SenderUserId, message.MessageText, message.IsInternal, message.CreatedAt);

    private static void EnsureTransition(ComplaintStatus current, ComplaintStatus target)
    {
        bool allowed = (current, target) switch
        {
            (ComplaintStatus.Submitted, ComplaintStatus.UnderReview) => true,
            (ComplaintStatus.UnderReview, ComplaintStatus.InProgress) => true,
            (ComplaintStatus.InProgress, ComplaintStatus.Resolved) => true,
            (ComplaintStatus.Resolved, ComplaintStatus.Closed) => true,
            (ComplaintStatus.UnderReview, ComplaintStatus.Closed) => true,
            (ComplaintStatus.InProgress, ComplaintStatus.Closed) => true,
            _ => false,
        };
        if (!allowed)
        {
            throw new OpsManager.Domain.Common.InvalidStateTransitionException(nameof(Complaint), current.ToString(), target.ToString());
        }
    }
}
