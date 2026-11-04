namespace SagaSales.IntegrationTests;

/// <summary>
/// Access to a real SQL Server for the integration suite.
/// <para>
/// Set <c>SAGASALES_TEST_SQL</c> to a connection string without a database, for example
/// <c>Server=localhost,1433;User Id=sa;Password=Your_password123;TrustServerCertificate=True</c>.
/// The suite appends <c>Database=saga_&lt;service&gt;_test</c> to it.
/// Without the variable, every test in this project returns early and says why: a machine with no
/// database should not report a red suite, but it must not report a false green either.
/// </para>
/// </summary>
public static class TestDatabase
{
    public const string VariableName = "SAGASALES_TEST_SQL";

    public static string? ConnectionBase { get; } = Environment.GetEnvironmentVariable(VariableName);

    public static bool IsAvailable => !string.IsNullOrWhiteSpace(ConnectionBase);

    public static string DatabaseName(string service) => $"saga_{service}_test";

    public static string ForService(string service)
        => $"{ConnectionBase!.TrimEnd(';')};Database={DatabaseName(service)}";

    /// <summary>Returns false and writes the reason when the suite cannot run.</summary>
    public static bool TryAnnounceSkip(string testName)
    {
        if (IsAvailable)
        {
            return false;
        }

        Console.WriteLine($"[skipped] {testName}: set {VariableName} to run integration tests against SQL Server.");
        return true;
    }
}
