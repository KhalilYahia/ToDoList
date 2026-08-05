using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpsManager.Domain.Common;
using OpsManager.Service.Common;

namespace OpsManager.Api.Infrastructure;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    private static readonly Action<ILogger, string, Exception?> LogUnhandledException =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1, nameof(ApiExceptionHandler)),
            "Unhandled API exception. TraceId: {TraceId}");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        int status = exception switch
        {
            RequestValidationException => StatusCodes.Status400BadRequest,
            ArgumentException => StatusCodes.Status400BadRequest,
            InvalidStateTransitionException => StatusCodes.Status409Conflict,
            DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            DomainInvariantException => StatusCodes.Status422UnprocessableEntity,
            AuthenticationFailedException => StatusCodes.Status401Unauthorized,
            ForbiddenAccessException => StatusCodes.Status403Forbidden,
            EntityNotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            TaskExecutionWindowException => StatusCodes.Status409Conflict,
            SubscriptionRestrictionException => StatusCodes.Status402PaymentRequired,
            _ => StatusCodes.Status500InternalServerError,
        };

        if (status >= 500)
        {
            LogUnhandledException(logger, httpContext.TraceIdentifier, exception);
        }

        ProblemDetails problem = exception is RequestValidationException validation
            ? new ValidationProblemDetails(
                validation.Errors.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal))
            : new ProblemDetails();
        problem.Status = status;
        problem.Title = GetTitle(status);
        problem.Detail = exception switch
        {
            DbUpdateConcurrencyException => "The resource was modified by another request. Reload it and try again.",
            _ when status >= 500 => "An unexpected error occurred.",
            _ => exception.Message,
        };
        problem.Instance = httpContext.Request.Path;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        if (exception is TaskExecutionWindowException windowEx)
        {
            problem.Extensions["code"] = windowEx.Code;
            problem.Extensions["scheduledStartAt"] = windowEx.ScheduledStartAt;
            problem.Extensions["dueAt"] = windowEx.DueAt;
        }
        else if (exception is ApplicationServiceException applicationException)
        {
            problem.Extensions["code"] = applicationException.Code;
        }
        else if (exception is DbUpdateConcurrencyException)
        {
            problem.Extensions["code"] = "concurrency_conflict";
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static string GetTitle(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Validation failed",
        StatusCodes.Status401Unauthorized => "Authentication failed",
        StatusCodes.Status402PaymentRequired => "Subscription restriction",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Resource not found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status422UnprocessableEntity => "Business rule violation",
        _ => "Internal server error",
    };
}
