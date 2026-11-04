namespace SagaSales.Shared.Exceptions;

public class SagaSalesException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public SagaSalesException(string code, string message, int statusCode = 400)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}

public class NotFoundException : SagaSalesException
{
    public NotFoundException(string resourceName, object key)
        : base("NotFound", $"{resourceName} with key '{key}' was not found", 404)
    {
    }
}

public class ValidationException : SagaSalesException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string message, IReadOnlyDictionary<string, string[]>? errors = null)
        : base("ValidationError", message, 400)
    {
        Errors = errors ?? new Dictionary<string, string[]>();
    }
}

public class UnauthorizedException : SagaSalesException
{
    public UnauthorizedException(string message = "Unauthorized access")
        : base("Unauthorized", message, 401)
    {
    }
}

public class ForbiddenException : SagaSalesException
{
    public ForbiddenException(string message = "Access forbidden")
        : base("Forbidden", message, 403)
    {
    }
}

public class ConflictException : SagaSalesException
{
    public ConflictException(string message)
        : base("Conflict", message, 409)
    {
    }
}

public class TenantNotFoundException : SagaSalesException
{
    public TenantNotFoundException(Guid tenantId)
        : base("TenantNotFound", $"Tenant with id '{tenantId}' was not found", 404)
    {
    }
}

public class InsufficientStockException : SagaSalesException
{
    public InsufficientStockException(string productName, int requested, int available)
        : base("InsufficientStock", $"Insufficient stock for '{productName}'. Requested: {requested}, Available: {available}", 409)
    {
    }
}

public class PaymentFailedException : SagaSalesException
{
    public PaymentFailedException(string message)
        : base("PaymentFailed", message, 402)
    {
    }
}