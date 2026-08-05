namespace OpsManager.Service.Common;

public abstract class ApplicationServiceException(string message, string code) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class RequestValidationException(
    IReadOnlyDictionary<string, string[]> errors,
    string message = "One or more validation errors occurred.")
    : ApplicationServiceException(message, "validation_failed")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class EntityNotFoundException(string entityName)
    : ApplicationServiceException($"{entityName} was not found.", "not_found");

public sealed class ForbiddenAccessException(string message = "You are not allowed to perform this action.")
    : ApplicationServiceException(message, "forbidden");

public sealed class AuthenticationFailedException(string message = "Authentication failed.")
    : ApplicationServiceException(message, "authentication_failed");

public sealed class ConflictException(string message, string code = "conflict")
    : ApplicationServiceException(message, code);

public sealed class SubscriptionRestrictionException(string message, string code = "subscription_restricted")
    : ApplicationServiceException(message, code);

public sealed class TaskExecutionWindowException(
    string message,
    string code,
    DateTimeOffset scheduledStartAt,
    DateTimeOffset dueAt)
    : ApplicationServiceException(message, code)
{
    public DateTimeOffset ScheduledStartAt { get; } = scheduledStartAt;
    public DateTimeOffset DueAt { get; } = dueAt;
}
