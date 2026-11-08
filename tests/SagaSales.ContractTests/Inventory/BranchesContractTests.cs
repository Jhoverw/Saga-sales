using System.Net;
using System.Net.Http.Json;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.ContractTests.Inventory;

/// <summary>
/// Contract of the branches (E-13): unique name per store, at most one default, and the default
/// branch cannot be deactivated. Requires a running stack.
/// </summary>
public class BranchesContractTests
{
    [Fact]
    public async Task A_branch_is_created_listed_and_updated()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_branch_is_created_listed_and_updated)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync();

        using var created = await session.SendAsync(HttpMethod.Post, "api/branches", new CreateBranchRequest
        {
            Name = "Centro",
            Address = "Av. 9 de Octubre 123",
            Phone = "555-0001",
            IsDefault = true,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var branch = await created.Content.ReadFromJsonAsync<BranchDto>();
        Assert.NotNull(branch);
        Assert.True(branch!.IsDefault);

        using var list = await session.SendAsync(HttpMethod.Get, "api/branches");
        var branches = await list.Content.ReadFromJsonAsync<List<BranchDto>>();
        Assert.Contains(branches!, b => b.Id == branch.Id && b.IsDefault);

        using var updated = await session.SendAsync(
            HttpMethod.Put,
            $"api/branches/{branch.Id}",
            new UpdateBranchRequest { Name = "Centro Norte", Address = "Otra", Phone = "555-0002" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var updatedBranch = await updated.Content.ReadFromJsonAsync<BranchDto>();
        Assert.Equal("Centro Norte", updatedBranch!.Name);
    }

    [Fact]
    public async Task A_duplicate_name_returns_409_and_the_default_cannot_be_deactivated()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_duplicate_name_returns_409_and_the_default_cannot_be_deactivated)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync();

        using var created = await session.SendAsync(HttpMethod.Post, "api/branches", new CreateBranchRequest
        {
            Name = "Única",
            IsDefault = true,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var branch = await created.Content.ReadFromJsonAsync<BranchDto>();

        using var duplicate = await session.SendAsync(HttpMethod.Post, "api/branches", new CreateBranchRequest
        {
            Name = "Única",
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var deactivateDefault = await session.SendAsync(HttpMethod.Delete, $"api/branches/{branch!.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deactivateDefault.StatusCode);
    }

    [Fact]
    public async Task A_non_default_branch_can_be_deactivated()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_non_default_branch_can_be_deactivated)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync(withBranch: true);

        using var created = await session.SendAsync(HttpMethod.Post, "api/branches", new CreateBranchRequest
        {
            Name = "Norte",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var branch = await created.Content.ReadFromJsonAsync<BranchDto>();

        using var deactivated = await session.SendAsync(HttpMethod.Delete, $"api/branches/{branch!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);

        using var list = await session.SendAsync(HttpMethod.Get, "api/branches?includeInactive=true");
        var branches = await list.Content.ReadFromJsonAsync<List<BranchDto>>();
        Assert.Contains(branches!, b => b.Id == branch.Id && b.Status == 2);
    }
}
