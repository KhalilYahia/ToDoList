using Microsoft.Extensions.DependencyInjection;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Auth;
using OpsManager.Service.Auth.DTOs;
using OpsManager.Service.Common;
using OpsManager.Service.Complaints;
using OpsManager.Service.Complaints.DTOs;
using OpsManager.Service.Infrastructure;
using OpsManager.Service.Notifications;
using OpsManager.Service.Orders;
using OpsManager.Service.Orders.DTOs;
using OpsManager.Service.Organizations;
using OpsManager.Service.Organizations.DTOs;
using OpsManager.Service.Platform;
using OpsManager.Service.Reports;
using OpsManager.Service.Tasks;
using OpsManager.Service.Tasks.DTOs;

namespace OpsManager.Service;

public static class DependencyInjection
{
    public static IServiceCollection AddOpsManagerServices(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISubscriptionAccessService, SubscriptionAccessService>();
        services.AddScoped<ITaskTemplateService, TaskTemplateService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<ITaskScheduleService, TaskScheduleService>();
        services.AddScoped<ITaskAssigneeResolver, TaskAssigneeResolver>();
        services.AddScoped<ITaskDistributionCreator, TaskDistributionCreator>();
        services.AddScoped<ITaskOccurrenceGeneratorService, TaskOccurrenceGeneratorService>();
        services.AddScoped<IOrderTemplateService, OrderTemplateService>();
        services.AddScoped<IDepartmentOrderService, DepartmentOrderService>();
        services.AddScoped<IComplaintService, ComplaintService>();
        services.AddScoped<IPlatformAuthService, PlatformAuthService>();
        services.AddScoped<IPlatformAdministrationService, PlatformAdministrationService>();
        services.AddScoped<IPlatformBranchService, PlatformBranchService>();
        services.AddScoped<ISubscriptionLifecycleService, SubscriptionLifecycleService>();
        services.AddScoped<IUserNotificationService, UserNotificationService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IOperationalNotificationDispatchService, OperationalNotificationDispatchService>();

        services.AddScoped<IRequestValidator<RegisterOrganizationRequest>, RegisterOrganizationValidator>();
        services.AddScoped<IRequestValidator<LoginRequest>, LoginValidator>();
        services.AddScoped<IRequestValidator<UpdateOrganizationRequest>, UpdateOrganizationValidator>();
        services.AddScoped<IRequestValidator<SaveBranchRequest>, SaveBranchValidator>();
        services.AddScoped<IRequestValidator<SaveDepartmentRequest>, SaveDepartmentValidator>();
        services.AddScoped<IRequestValidator<CreateMemberRequest>, CreateMemberValidator>();
        services.AddScoped<IRequestValidator<UpdateMemberRequest>, UpdateMemberValidator>();
        services.AddScoped<IRequestValidator<SaveTaskTemplateRequest>, SaveTaskTemplateValidator>();
        services.AddScoped<IRequestValidator<CreateTaskRequest>, CreateTaskValidator>();
        services.AddScoped<IRequestValidator<SaveTaskScheduleRequest>, SaveTaskScheduleValidator>();
        services.AddScoped<IRequestValidator<SaveOrderTemplateRequest>, SaveOrderTemplateValidator>();
        services.AddScoped<IRequestValidator<CreateDepartmentOrderRequest>, CreateDepartmentOrderValidator>();
        services.AddScoped<IRequestValidator<CreateComplaintRequest>, CreateComplaintValidator>();
        services.AddScoped<IRequestValidator<UpdateComplaintRequest>, UpdateComplaintValidator>();
        return services;
    }
}
