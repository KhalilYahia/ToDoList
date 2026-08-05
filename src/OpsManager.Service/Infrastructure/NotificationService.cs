using OpsManager.Domain.Constants;
using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Infrastructure;

public sealed class NotificationService(IUnitOfWork unitOfWork, IClock clock) : INotificationService
{
    public async Task CreateAsync(
        Guid organizationId,
        Guid userId,
        NotificationType type,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? parameters = null,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null,
        CancellationToken cancellationToken = default)
    {
        User? user = await unitOfWork.Repository<User>().GetByIdAsync(userId, cancellationToken);
        Organization? organization =
            await unitOfWork.Repository<Organization>().GetByIdAsync(organizationId, cancellationToken);
        string language =
            user?.PreferredLanguage ?? organization?.DefaultLanguage ?? SupportedLanguages.English;
        if (!SupportedLanguages.All.Contains(language))
        {
            language = organization?.DefaultLanguage ?? SupportedLanguages.English;
        }

        Dictionary<string, string> finalParameters = parameters is null
            ? new(StringComparer.Ordinal)
            : new(parameters, StringComparer.Ordinal);
        finalParameters["language"] = language;
        await unitOfWork.Repository<Notification>().AddAsync(new Notification
        {
            OrganizationId = organizationId,
            UserId = userId,
            NotificationType = type,
            Parameters = finalParameters,
            Title = LocalizeTitle(title, language),
            Body = body,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            CreatedAt = clock.UtcNow,
        }, cancellationToken);
    }

    private static string LocalizeTitle(string title, string language) =>
        (language, title) switch
        {
            (SupportedLanguages.Russian, "Task assigned") => "Назначена задача",
            (SupportedLanguages.Russian, "Task returned") => "Задача возвращена",
            (SupportedLanguages.Russian, "Task due soon") => "Срок задачи приближается",
            (SupportedLanguages.Russian, "Task overdue") => "Задача просрочена",
            (SupportedLanguages.Russian, "Order assigned") => "Назначен заказ",
            (SupportedLanguages.Russian, "Order Accept") => "Заказ принят",
            (SupportedLanguages.Russian, "Order MarkReady") => "Заказ готов",
            (SupportedLanguages.Russian, "Order Deliver") => "Заказ доставлен",
            (SupportedLanguages.Russian, "Complaint assigned") => "Жалоба назначена",
            (SupportedLanguages.Russian, "Complaint response") => "Ответ на жалобу",
            (SupportedLanguages.Russian, "Complaint updated") => "Жалоба обновлена",
            (SupportedLanguages.Russian, "Subscription expiring") => "Подписка скоро истекает",
            (SupportedLanguages.Russian, "Subscription expired") => "Подписка истекла",
            (SupportedLanguages.Arabic, "Task assigned") => "تم تعيين مهمة",
            (SupportedLanguages.Arabic, "Task returned") => "تمت إعادة المهمة",
            (SupportedLanguages.Arabic, "Task due soon") => "موعد المهمة قريب",
            (SupportedLanguages.Arabic, "Task overdue") => "المهمة متأخرة",
            (SupportedLanguages.Arabic, "Order assigned") => "تم تعيين الطلب",
            (SupportedLanguages.Arabic, "Complaint assigned") => "تم تعيين الشكوى",
            (SupportedLanguages.Arabic, "Complaint response") => "رد على الشكوى",
            (SupportedLanguages.Arabic, "Complaint updated") => "تم تحديث الشكوى",
            (SupportedLanguages.Arabic, "Subscription expiring") => "سينتهي الاشتراك قريبًا",
            (SupportedLanguages.Arabic, "Subscription expired") => "انتهى الاشتراك",
            _ => title,
        };
}
