namespace SagaSales.BuildingBlocks.Data;

using System.Data.Common;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Translates the error codes raised by the stored procedures into an HTTP outcome.
/// The procedures raise <c>raise exception 'CODE: message'</c>, so the code is the prefix of the
/// message. Mapping by prefix (instead of by provider exception type) keeps this layer free of
/// any database-provider dependency.
/// </summary>
public static class SqlErrorMapper
{
    private static readonly Dictionary<string, (string Code, int Status)> Known =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["NOT_FOUND"] = ("NotFound", StatusCodes.Status404NotFound),
            ["CONFLICT"] = ("Conflict", StatusCodes.Status409Conflict),
            ["INSUFFICIENT_STOCK"] = ("InsufficientStock", StatusCodes.Status409Conflict),
            ["INVALID_TRANSITION"] = ("InvalidTransition", StatusCodes.Status409Conflict),
            ["PAYMENT_ALREADY_EXISTS"] = ("PaymentAlreadyExists", StatusCodes.Status409Conflict),
            ["REFUND_EXCEEDS_REFUNDABLE"] = ("RefundExceedsRefundable", StatusCodes.Status409Conflict),
            ["VALIDATION_ERROR"] = ("ValidationError", StatusCodes.Status400BadRequest),
            ["UNAUTHORIZED"] = ("Unauthorized", StatusCodes.Status401Unauthorized),
            ["FORBIDDEN"] = ("Forbidden", StatusCodes.Status403Forbidden),
        };

    /// <summary>Returns <c>true</c> when the exception carries a code this layer understands.</summary>
    public static bool TryMap(Exception exception, out string code, out int statusCode)
    {
        code = string.Empty;
        statusCode = StatusCodes.Status500InternalServerError;

        if (exception is not DbException)
        {
            return false;
        }

        var message = exception.Message;
        var separator = message.IndexOf(':', StringComparison.Ordinal);
        var candidate = (separator > 0 ? message[..separator] : message).Trim();

        if (!Known.TryGetValue(candidate, out var mapped))
        {
            return false;
        }

        code = mapped.Code;
        statusCode = mapped.Status;
        return true;
    }
}

public static class SqlErrorMapperExtensions
{
    /// <summary>Message for the client: the code prefix is stripped, the detail is kept.</summary>
    public static string ToClientMessage(this DbException exception)
    {
        var message = exception.Message;
        var separator = message.IndexOf(':', StringComparison.Ordinal);
        return separator > 0 && separator + 1 < message.Length
            ? message[(separator + 1)..].Trim()
            : message;
    }
}
