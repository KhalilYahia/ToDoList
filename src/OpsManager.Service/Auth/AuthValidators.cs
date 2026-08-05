using OpsManager.Service.Auth.DTOs;
using OpsManager.Service.Common;

namespace OpsManager.Service.Auth;

public sealed class RegisterOrganizationValidator : RequestValidator<RegisterOrganizationRequest>
{
    protected override void Validate(RegisterOrganizationRequest request)
    {
        Required(request.OrganizationName, nameof(request.OrganizationName), 200);
        Optional(request.LegalName, nameof(request.LegalName), 240);
        Timezone(request.Timezone, nameof(request.Timezone));
        SupportedLanguage(request.DefaultLanguage, nameof(request.DefaultLanguage));
        Required(request.ManagerFullName, nameof(request.ManagerFullName), 200);
        Email(request.ManagerEmail, nameof(request.ManagerEmail));
        Password(request.Password, nameof(request.Password));
        Optional(request.Phone, nameof(request.Phone), 40);
    }
}

public sealed class LoginValidator : RequestValidator<LoginRequest>
{
    protected override void Validate(LoginRequest request)
    {
        if (request.OrganizationId.HasValue && request.OrganizationId.Value == Guid.Empty)
        {
            Add(nameof(request.OrganizationId), "OrganizationId cannot be empty when supplied.");
        }

        Email(request.Email, nameof(request.Email));
        Required(request.Password, nameof(request.Password), 1000);
    }
}
