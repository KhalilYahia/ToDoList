using System.Text.RegularExpressions;
using OpsManager.Domain.Constants;

namespace OpsManager.Service.Common;

public interface IRequestValidator<in TRequest>
{
    void ValidateAndThrow(TRequest request);
}

public abstract partial class RequestValidator<TRequest> : IRequestValidator<TRequest>
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public void ValidateAndThrow(TRequest request)
    {
        _errors.Clear();
        Validate(request);
        if (_errors.Count > 0)
        {
            throw new RequestValidationException(
                _errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal));
        }
    }

    protected abstract void Validate(TRequest request);

    protected void Required(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(field, $"{field} is required.");
        }
        else if (value.Trim().Length > maxLength)
        {
            Add(field, $"{field} cannot exceed {maxLength} characters.");
        }
    }

    protected void Optional(string? value, string field, int maxLength)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Trim().Length > maxLength)
        {
            Add(field, $"{field} cannot exceed {maxLength} characters.");
        }
    }

    protected void Email(string? value, string field)
    {
        Required(value, field, 320);
        if (!string.IsNullOrWhiteSpace(value) && !EmailPattern().IsMatch(value.Trim()))
        {
            Add(field, $"{field} must be a valid email address.");
        }
    }

    protected void Password(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 12 ||
            !value.Any(char.IsUpper) || !value.Any(char.IsLower) || !value.Any(char.IsDigit))
        {
            Add(field, $"{field} must contain at least 12 characters with upper-case, lower-case, and numeric characters.");
        }
    }

    protected void SupportedLanguage(string? value, string field)
    {
        if (value is null || !SupportedLanguages.All.Contains(value, StringComparer.Ordinal))
        {
            Add(field, $"{field} must be ar, en, or ru.");
        }
    }

    protected void Timezone(string? value, string field)
    {
        Required(value, field, 100);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(value);
        }
        catch (TimeZoneNotFoundException)
        {
            Add(field, $"{field} is not a recognized system timezone.");
        }
        catch (InvalidTimeZoneException)
        {
            Add(field, $"{field} is invalid.");
        }
    }

    protected void Positive(decimal value, string field, bool allowZero = false)
    {
        if (allowZero ? value < 0 : value <= 0)
        {
            Add(field, allowZero ? $"{field} cannot be negative." : $"{field} must be positive.");
        }
    }

    protected void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out List<string>? messages))
        {
            messages = [];
            _errors.Add(field, messages);
        }

        messages.Add(message);
    }

    [GeneratedRegex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
