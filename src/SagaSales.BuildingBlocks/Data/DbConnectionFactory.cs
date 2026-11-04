namespace SagaSales.BuildingBlocks.Data;

using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Creates connections to the service's own database. No service ever opens another's.
/// The platform targets SQL Server; every procedure is T-SQL.
/// </summary>
public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();

    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed class SqlServerConnectionFactory : IDbConnectionFactory
{
    public const string ConnectionStringName = "DefaultConnection";

    private readonly string _connectionString;

    public SqlServerConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Missing connection string '{ConnectionStringName}'. Set it in appsettings.json or as ConnectionStrings__{ConnectionStringName}.");
    }

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
