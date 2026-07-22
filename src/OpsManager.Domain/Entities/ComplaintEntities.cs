using OpsManager.Domain.Common;
using OpsManager.Domain.Enums;

namespace OpsManager.Domain.Entities;

public sealed class Complaint : TenantSoftDeletableEntity
{
    private Complaint() { }

    public Complaint(
        Guid organizationId,
        Guid branchId,
        string complaintNumber,
        Guid submittedBy,
        string title,
        string description,
        ComplaintVisibility visibility)
    {
        OrganizationId = organizationId;
        BranchId = branchId;
        ComplaintNumber = Guard.Required(complaintNumber, nameof(complaintNumber), 64);
        SubmittedBy = submittedBy;
        Title = Guard.Required(title, nameof(title), 240);
        Description = Guard.Required(description, nameof(description), 8000);
        Visibility = visibility;
    }

    public Guid BranchId { get; set; }
    public string ComplaintNumber { get; set; } = string.Empty;
    public Guid SubmittedBy { get; set; }
    public Guid? TargetDepartmentId { get; set; }
    public Guid? AssignedTo { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ComplaintStatus Status { get; set; } = ComplaintStatus.Submitted;
    public ComplaintVisibility Visibility { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}

public sealed class ComplaintMessage : TenantAuditableEntity
{
    public Guid ComplaintId { get; set; }
    public Guid SenderUserId { get; set; }
    public string MessageText { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
}

public sealed class ComplaintAttachment : TenantAuditableEntity
{
    public Guid ComplaintId { get; set; }
    public Guid? ComplaintMessageId { get; set; }
    public Guid UploadedBy { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
}
