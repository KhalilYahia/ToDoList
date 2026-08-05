using OpsManager.Service.Common;
using OpsManager.Service.Complaints.DTOs;

namespace OpsManager.Service.Complaints;

public sealed class CreateComplaintValidator : RequestValidator<CreateComplaintRequest>
{
    protected override void Validate(CreateComplaintRequest request)
    {
        if (request.BranchId == Guid.Empty)
        {
            Add(nameof(request.BranchId), "BranchId is required.");
        }

        Required(request.Title, nameof(request.Title), 240);
        Required(request.Description, nameof(request.Description), 8000);
    }
}

public sealed class UpdateComplaintValidator : RequestValidator<UpdateComplaintRequest>
{
    protected override void Validate(UpdateComplaintRequest request)
    {
        Required(request.Title, nameof(request.Title), 240);
        Required(request.Description, nameof(request.Description), 8000);
    }
}
