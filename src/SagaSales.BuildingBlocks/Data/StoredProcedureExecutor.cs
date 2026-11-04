namespace SagaSales.BuildingBlocks.Data;

using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Logging;
using SagaSales.BuildingBlocks.Extensions;

/// <summary>
/// Runs stored procedures. Two rules are baked in so they cannot be forgotten:
/// <list type="bullet">
/// <item>the tenant is always passed as <c>p_tenant_id</c> (constitution §V);</item>
/// <item>one procedure is one transaction — multi-table writes live inside the procedure.</item>
/// </list>
/// </summary>
public interface IStoredProcedureExecutor
{
    Task<IReadOnlyList<T>> QueryProcAsync<T>(
        string procedure,
        DynamicParameters? parameters = null,
        bool tenantScoped = true,
        CancellationToken cancellationToken = default);

    Task<T?> QueryProcSingleAsync<T>(
        string procedure,
        DynamicParameters? parameters = null,
        bool tenantScoped = true,
        CancellationToken cancellationToken = default);

    Task<int> ExecuteProcAsync(
        string procedure,
        DynamicParameters? parameters = null,
        bool tenantScoped = true,
        CancellationToken cancellationToken = default);

    Task<T?> ExecuteScalarProcAsync<T>(
        string procedure,
        DynamicParameters? parameters = null,
        bool tenantScoped = true,
        CancellationToken cancellationToken = default);
}

public sealed class StoredProcedureExecutor(
    IDbConnectionFactory connectionFactory,
    ICurrentUser currentUser,
    ILogger<StoredProcedureExecutor> logger) : IStoredProcedureExecutor
{
    public const string TenantParameter = "p_tenant_id";

    public async Task<IReadOnlyList<T>> QueryProcAsync<T>(
        string procedure,
        DynamicParameters? parameters = null,
        bool tenantScoped = true,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var arguments = WithTenant(parameters, tenantScoped);
        var rows = await connection.QueryAsync<T>(Build(procedure, arguments, cancellationToken));
        return rows.AsList();
    }

    public async Task<T?> QueryProcSingleAsync<T>(
        string procedure,
        DynamicParameters? parameters = null,
        bool tenantScoped = true,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var arguments = WithTenant(parameters, tenantScoped);
        var rows = await connection.QueryAsync<T>(Build(procedure, arguments, cancellationToken));
        return rows.FirstOrDefault();
    }

    public async Task<int> ExecuteProcAsync(
        string procedure,
        DynamicParameters? parameters = null,
        bool tenantScoped = true,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var arguments = WithTenant(parameters, tenantScoped);
        return await connection.ExecuteAsync(Build(procedure, arguments, cancellationToken));
    }

    public async Task<T?> ExecuteScalarProcAsync<T>(
        string procedure,
        DynamicParameters? parameters = null,
        bool tenantScoped = true,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var arguments = WithTenant(parameters, tenantScoped);
        return await connection.ExecuteScalarAsync<T>(Build(procedure, arguments, cancellationToken));
    }

    private async Task<DbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = (DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        logger.LogDebug("Opened a database connection for a procedure call");
        return connection;
    }

    private DynamicParameters WithTenant(DynamicParameters? parameters, bool tenantScoped)
    {
        var arguments = parameters ?? new DynamicParameters();

        if (!tenantScoped)
        {
            return arguments;
        }

        if (arguments.ParameterNames.Contains(TenantParameter))
        {
            return arguments;
        }

        arguments.Add(TenantParameter, currentUser.RequireTenantId());
        return arguments;
    }

    private static CommandDefinition Build(
        string procedure,
        DynamicParameters arguments,
        CancellationToken cancellationToken) => new(
        procedure,
        arguments,
        commandType: CommandType.StoredProcedure,
        cancellationToken: cancellationToken);
}

/// <summary>Small helpers so callers do not repeat the parameter plumbing.</summary>
public static class SqlArgs
{
    public static DynamicParameters New() => new();

    public static DynamicParameters From(object template) => new(template);

    public static DynamicParameters With(this DynamicParameters parameters, string name, object? value)
    {
        parameters.Add(name, value);
        return parameters;
    }
}
