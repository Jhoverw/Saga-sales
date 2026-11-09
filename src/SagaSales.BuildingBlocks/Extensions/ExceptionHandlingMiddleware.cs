namespace SagaSales.BuildingBlocks.Extensions;

using System.Data.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SagaSales.BuildingBlocks.Data;
using SagaSales.Shared.Exceptions;

/// <summary>
/// Turns domain exceptions — and the error codes raised by the stored procedures — into consistent
/// JSON responses, so controllers do not repeat try/catch per endpoint.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            // The field-level reasons are the useful part: without them the client can only say
            // "validation failed" and the operator has no idea which box to fix.
            logger.LogWarning(
                ex,
                "Validation error on {Path}: {Errors}",
                context.Request.Path,
                string.Join("; ", ex.Errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}")));

            await WriteAsync(context, ex.StatusCode, ex.Code, ex.Message, ex.Errors);
        }
        catch (SagaSalesException ex)
        {
            logger.LogWarning(ex, "Domain error {Code} on {Path}", ex.Code, context.Request.Path);
            await WriteAsync(context, ex.StatusCode, ex.Code, ex.Message);
        }
        catch (DbException ex) when (SqlErrorMapper.TryMap(ex, out var code, out var statusCode))
        {
            // A procedure raised a known code: this is a business outcome, not a server failure.
            logger.LogWarning(ex, "Database domain error {Code} on {Path}", code, context.Request.Path);
            await WriteAsync(context, statusCode, code, ex.ToClientMessage());
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "Unhandled database error on {Path}", context.Request.Path);
            await WriteAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "DatabaseError",
                "The request could not be completed.");
        }
        catch (BadHttpRequestException ex)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, "BadRequest", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error on {Path}", context.Request.Path);
            await WriteAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "InternalServerError",
                "An unexpected error occurred.");
        }
    }

    private static async Task WriteAsync(
        HttpContext context,
        int statusCode,
        string code,
        string message,
        object? details = null)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;

        if (details is null)
        {
            await context.Response.WriteAsJsonAsync(new { code, message });
            return;
        }

        await context.Response.WriteAsJsonAsync(new { code, message, errors = details });
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseSagaSalesExceptionHandling(this IApplicationBuilder builder)
        => builder.UseMiddleware<ExceptionHandlingMiddleware>();
}
