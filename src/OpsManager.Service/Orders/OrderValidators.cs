using OpsManager.Domain.Enums;
using OpsManager.Service.Common;
using OpsManager.Service.Orders.DTOs;

namespace OpsManager.Service.Orders;

public static class OrderValidation
{
    public static void ValidateItem(OrderTemplateItemRequest item)
    {
        if (string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 200)
        {
            throw Validation(nameof(item.Name), "Item name is required and cannot exceed 200 characters.");
        }

        if (item.UnitCode == UnitCode.Custom && string.IsNullOrWhiteSpace(item.CustomUnitLabel))
        {
            throw Validation(nameof(item.CustomUnitLabel), "A custom unit label is required.");
        }

        if (item.DefaultQuantity < 0 || item.MinimumQuantity < 0)
        {
            throw Validation("quantity", "Template quantities cannot be negative.");
        }
    }

    public static RequestValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

public sealed class SaveOrderTemplateValidator : RequestValidator<SaveOrderTemplateRequest>
{
    protected override void Validate(SaveOrderTemplateRequest request)
    {
        Required(request.Name, nameof(request.Name), 200);
        Optional(request.Description, nameof(request.Description), 4000);
        if (request.BranchId == Guid.Empty ||
            request.SourceDepartmentId == Guid.Empty ||
            request.TargetDepartmentId == Guid.Empty)
        {
            Add("scope", "Branch, source department, and target department are required.");
        }

        if (request.SourceDepartmentId == request.TargetDepartmentId)
        {
            Add(nameof(request.TargetDepartmentId), "Source and target departments must differ.");
        }

        foreach (OrderTemplateItemRequest item in request.Items)
        {
            OrderValidation.ValidateItem(item);
        }

        if (request.Items.Select(item => item.SortOrder).Distinct().Count() != request.Items.Count)
        {
            Add(nameof(request.Items), "Item sort-order values must be unique.");
        }
    }
}

public sealed class CreateDepartmentOrderValidator : RequestValidator<CreateDepartmentOrderRequest>
{
    protected override void Validate(CreateDepartmentOrderRequest request)
    {
        if (request.BranchId == Guid.Empty ||
            request.SourceDepartmentId == Guid.Empty ||
            request.TargetDepartmentId == Guid.Empty)
        {
            Add("scope", "Branch, source department, and target department are required.");
        }

        if (request.SourceDepartmentId == request.TargetDepartmentId)
        {
            Add(nameof(request.TargetDepartmentId), "Source and target departments must differ.");
        }

        if (request.Items.Count == 0)
        {
            Add(nameof(request.Items), "At least one order item is required.");
        }

        if (request.Items.Any(item => item.RequestedQuantity < 0))
        {
            Add(nameof(request.Items), "Requested quantities cannot be negative.");
        }
    }
}
