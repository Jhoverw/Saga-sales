using System.Text.RegularExpressions;
using Xunit;

namespace SagaSales.UnitTests.Architecture;

/// <summary>
/// Static checks that enforce constitution rules which no compiler verifies:
/// §V (tenant isolation in every stored procedure), §VIII (no ORM) and the .NET 10 runtime.
/// They read repository files, so they run without SQL Server or Docker.
/// </summary>
// Known debt: these fail today on purpose (see the commit message). They run when asked for
// explicitly with --filter "Category=KnownDebt", and CI excludes them until the debt is paid.
[Trait("Category", "KnownDebt")]
public class ConstitutionComplianceTests
{
    // Procedures that legitimately run before a tenant is known. Every entry needs a reason.
    // an entry here is a documented exception, not a way to silence a finding.
    private static readonly Dictionary<string, string> GlobalProcedureAllowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sp_tenant_create"] = "bootstrap: creates the tenant itself",
        ["sp_tenant_get_by_slug"] = "resolves the tenant from the store slug at sign-in",
        ["sp_user_find_by_email"] = "sign-in lookup before the tenant claim exists; confirm the email is unique per tenant",
        ["sp_order_holds_due"] = "spec 008: the expiry sweep lists every store's expired holds; each is cancelled in its own store",
    };

    private static readonly Regex CreateProcedure = new(
        @"CREATE\s+(?:OR\s+ALTER\s+)?PROCEDURE\s+(?:\[?dbo\]?\.)?\[?(?<name>\w+)\]?(?<params>.*?)^\s*AS\b",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Multiline);

    private static readonly Regex ParameterName = new(@"@(\w+)", RegexOptions.IgnoreCase);

    [Fact]
    public void EveryStoredProcedureTakesTenantAsFirstParameter()
    {
        var root = RepositoryRoot();
        var sqlFiles = Directory.GetFiles(Path.Combine(root, "db"), "*.sql", SearchOption.AllDirectories);

        var scanned = 0;
        var violations = new List<string>();

        foreach (var file in sqlFiles)
        {
            var text = File.ReadAllText(file);
            foreach (Match procedure in CreateProcedure.Matches(text))
            {
                scanned++;
                var name = procedure.Groups["name"].Value;
                if (GlobalProcedureAllowlist.ContainsKey(name))
                {
                    continue;
                }

                var first = ParameterName.Match(procedure.Groups["params"].Value);
                var isTenantFirst = first.Success
                    && first.Groups[1].Value.Equals("p_tenant_id", StringComparison.OrdinalIgnoreCase);

                if (!isTenantFirst)
                {
                    var actual = first.Success ? "@" + first.Groups[1].Value : "no parameters";
                    violations.Add($"{Path.GetRelativePath(root, file)}: {name} starts with {actual}");
                }
            }
        }

        // Guard against a scanner that silently matches nothing and passes for the wrong reason.
        Assert.True(scanned >= 50, $"Expected to scan at least 50 procedures, found {scanned}.");
        Assert.True(
            violations.Count == 0,
            "Stored procedures must take p_tenant_id as first parameter (constitution §V):\n" + string.Join("\n", violations));
    }

    [Fact]
    public void NoProjectReferencesEntityFrameworkCore()
    {
        var root = RepositoryRoot();
        var offenders = ProjectFiles(root)
            .Where(p => File.ReadAllText(p).Contains("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase))
            .Select(p => Path.GetRelativePath(root, p))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "EF Core is forbidden (constitution §VIII). Remove these references:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void EveryProjectTargetsNet10()
    {
        var root = RepositoryRoot();
        var offenders = ProjectFiles(root)
            .Where(p => !File.ReadAllText(p).Contains("<TargetFramework>net10.0</TargetFramework>", StringComparison.Ordinal))
            .Select(p => Path.GetRelativePath(root, p))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "All projects must target net10.0 (constitution Technology Constraints):\n" + string.Join("\n", offenders));
    }

    private static IEnumerable<string> ProjectFiles(string root) =>
        new[] { "src", "tests" }
            .SelectMany(folder => Directory.GetFiles(Path.Combine(root, folder), "*.csproj", SearchOption.AllDirectories))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SagaSales.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException(
            $"SagaSales.sln not found above {AppContext.BaseDirectory}");
    }
}
