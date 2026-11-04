namespace SagaSales.BuildingBlocks.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SagaSales.Shared.Exceptions;

/// <summary>
/// Turns domain exceptions into consistent JSON problem responses, so controllers do not
/// repeat try/catch blocks per endpoint.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (SagaSalesException ex)
        {
            logger.LogWarning(ex, "Domain error {Code} on {Path}", ex.Code, context.Request.Path);
            await WriteAsync(context, ex.StatusCode, ex.Code, ex.Message);
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

    private static async Task WriteAsync(HttpContext context, int statusCode, string code, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { code, message });
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseSagaSalesExceptionHandling(this IApplicationBuilder builder)
        => builder.UseMiddleware<ExceptionHandlingMiddleware>();
}
