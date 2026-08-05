using OpsManager.Domain.Enums;
using OpsManager.Service.Common;

namespace OpsManager.Service.Complaints.DTOs;

public sealed record CreateComplaintRequest(
    Guid BranchId,
    Guid? TargetDepartmentId,
    string Title,
    string Description,
    ComplaintVisibility Visibility);

public sealed record UpdateComplaintRequest(
    string Title,
    string Description,
    ComplaintVisibility Visibility);

public sealed record ComplaintMessageRequest(string Message, bool IsInternal = false);
public sealed record AssignComplaintRequest(Guid AssigneeUserId);

public sealed record ComplaintMessageDto(
    Guid Id,
    Guid SenderUserId,
    string Message,
    bool IsInternal,
    DateTimeOffset CreatedAt);

public sealed record ComplaintDto(
    Guid Id,
    string ComplaintNumber,
    Guid BranchId,
    Guid SubmittedBy,
    Guid? TargetDepartmentId,
    Guid? AssignedTo,
    string Title,
    string Description,
    ComplaintStatus Status,
    ComplaintVisibility Visibility,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<ComplaintMessageDto> Messages);

public sealed record ComplaintQuery(
    int Page = 1,
    int PageSize = 20,
    ComplaintStatus? Status = null,
    Guid? BranchId = null,
    Guid? DepartmentId = null,
    Guid? AssigneeUserId = null)
{
    public PageQuery PageQuery => new(Page, PageSize);
}
