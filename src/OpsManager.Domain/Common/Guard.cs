namespace OpsManager.Domain.Common;

public static class Guard
{
    public static Guid NotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new DomainInvariantException($"{parameterName} cannot be empty.");
        }

        return value;
    }

    public static int Positive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new DomainInvariantException($"{parameterName} must be positive.");
        }

        return value;
    }

    public static int NonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new DomainInvariantException($"{parameterName} cannot be negative.");
        }

        return value;
    }

    public static DateTimeOffset NotDefault(DateTimeOffset value, string parameterName)
    {
        if (value == default)
        {
            throw new DomainInvariantException($"{parameterName} cannot be the default value.");
        }

        return value;
    }

    public static string Required(string? value, string parameterName, int maxLength = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainInvariantException($"{parameterName} is required.");
        }

        string trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainInvariantException($"{parameterName} cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }

    public static string? Optional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainInvariantException($"Value cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }

    public static void SupportedLanguage(string value, string parameterName)
    {
        if (!Constants.SupportedLanguages.All.Contains(value, StringComparer.Ordinal))
        {
            throw new DomainInvariantException($"{parameterName} must be ar, en, or ru.");
        }
    }
}
