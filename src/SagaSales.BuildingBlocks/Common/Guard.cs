namespace SagaSales.BuildingBlocks.Common;

public static class Guard
{
    public static void AgainstNull<T>(T? value, string parameterName)
        where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }
    }

    public static void AgainstNullOrEmpty(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} cannot be null or empty", parameterName);
        }
    }

    public static void AgainstEmptyGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"{parameterName} must not be an empty identifier", parameterName);
        }
    }

    public static void AgainstEmpty<T>(IReadOnlyCollection<T>? value, string parameterName)
    {
        if (value is null || value.Count == 0)
        {
            throw new ArgumentException($"{parameterName} must contain at least one element", parameterName);
        }
    }

    public static void AgainstNegativeOrZero(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} must be greater than zero");
        }
    }

    public static void AgainstNegativeOrZero(decimal value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} must be greater than zero");
        }
    }

    public static void AgainstNegative(decimal value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} must not be negative");
        }
    }

    public static void AgainstOutOfRange<T>(T value, T min, T max, string parameterName)
        where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} must be between {min} and {max}");
        }
    }
}
