using OpsManager.Domain.Enums;
using OpsManager.Service.Common;
using OpsManager.Service.Organizations.DTOs;

namespace OpsManager.Service.Organizations;

public sealed class UpdateOrganizationValidator : RequestValidator<UpdateOrganizationRequest>
{
    protected override void Validate(UpdateOrganizationRequest request)
    {
        Required(request.Name, nameof(request.Name), 200);
        Optional(request.LegalName, nameof(request.LegalName), 240);
        Optional(request.LogoUrl, nameof(request.LogoUrl), 2048);
        Optional(request.Phone, nameof(request.Phone), 40);
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            Email(request.Email, nameof(request.Email));
        }

        Timezone(request.Timezone, nameof(request.Timezone));
        SupportedLanguage(request.DefaultLanguage, nameof(request.DefaultLanguage));
    }
}

public sealed class SaveBranchValidator : RequestValidator<SaveBranchRequest>
{
    protected override void Validate(SaveBranchRequest request)
    {
        Required(request.Name, nameof(request.Name), 160);
        Optional(request.Address, nameof(request.Address), 1000);
        Optional(request.Phone, nameof(request.Phone), 40);
        Timezone(request.Timezone, nameof(request.Timezone));
    }
}

public sealed class SaveDepartmentValidator : RequestValidator<SaveDepartmentRequest>
{
    protected override void Validate(SaveDepartmentRequest request)
    {
        if (request.BranchId == Guid.Empty)
        {
            Add(nameof(request.BranchId), "BranchId is required.");
        }

        Required(request.Name, nameof(request.Name), 160);
        Optional(request.Description, nameof(request.Description), 2000);
    }
}

public sealed class CreateMemberValidator : RequestValidator<CreateMemberRequest>
{
    protected override void Validate(CreateMemberRequest request)
    {
        Required(request.FullName, nameof(request.FullName), 200);
        Email(request.Email, nameof(request.Email));
        Optional(request.Phone, nameof(request.Phone), 40);
        SupportedLanguage(request.PreferredLanguage, nameof(request.PreferredLanguage));
        Password(request.TemporaryPassword, nameof(request.TemporaryPassword));
        if (!Enum.IsDefined(request.Role))
        {
            Add(nameof(request.Role), "Role is invalid.");
        }
    }
}

public sealed class UpdateMemberValidator : RequestValidator<UpdateMemberRequest>
{
    protected override void Validate(UpdateMemberRequest request)
    {
        Required(request.FullName, nameof(request.FullName), 200);
        Optional(request.Phone, nameof(request.Phone), 40);
        SupportedLanguage(request.PreferredLanguage, nameof(request.PreferredLanguage));
        if (!Enum.IsDefined(request.Role))
        {
            Add(nameof(request.Role), "Role is invalid.");
        }
    }
}
