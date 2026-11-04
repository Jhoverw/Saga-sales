namespace SagaSales.ContractTests;

/// <summary>
/// Access to a running gateway for the contract suite.
/// <para>
/// Set <c>SAGASALES_GATEWAY_URL</c> (for example <c>http://localhost:5000</c>) with the stack up.
/// Without it the tests return early and say why, so a laptop without services does not show a red
/// suite — and does not show a false green either.
/// </para>
/// </summary>
public static class Gateway
{
    public const string VariableName = "SAGASALES_GATEWAY_URL";

    private static readonly string? BaseUrl = Environment.GetEnvironmentVariable(VariableName);

    public static bool IsAvailable => !string.IsNullOrWhiteSpace(BaseUrl);

    public static HttpClient CreateClient() => new()
    {
        BaseAddress = new Uri(BaseUrl!.TrimEnd('/') + "/"),
        Timeout = TimeSpan.FromSeconds(15),
    };

    public static bool TryAnnounceSkip(string testName)
    {
        if (IsAvailable)
        {
            return false;
        }

        Console.WriteLine($"[skipped] {testName}: set {VariableName} to run contract tests against a live gateway.");
        return true;
    }
}
