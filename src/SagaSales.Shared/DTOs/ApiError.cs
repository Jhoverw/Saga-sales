namespace SagaSales.Shared.DTOs;

/// <summary>Shape of the error body returned by the API (see ExceptionHandlingMiddleware).</summary>
public class ApiError
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
