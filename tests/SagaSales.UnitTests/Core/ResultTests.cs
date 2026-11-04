using SagaSales.Shared.Contracts;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.UnitTests.Core;

public class ResultTests
{
    [Fact]
    public void Success_HasNoErrors()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Failure_CarriesTheError()
    {
        var error = new Error("NotFound", "Product was not found");

        var result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void Failure_WithManyErrors_KeepsAllOfThem()
    {
        var errors = new List<Error>
        {
            new("Name", "Name is required"),
            new("Price", "Price must be positive"),
        };

        var result = Result.Failure(errors);

        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void ImplicitConversion_FromError_ProducesAFailure()
    {
        Result result = new Error("Conflict", "already exists");

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
    }

    [Fact]
    public void TypedResult_ExposesTheValue_OnSuccess()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void TypedResult_OnFailure_HasTheDefaultValue()
    {
        var result = Result<int>.Failure(new Error("E", "failed"));

        Assert.True(result.IsFailure);
        Assert.Equal(0, result.Value);
    }
}

public class PagedResultTests
{
    [Fact]
    public void TotalPages_IsRoundedUp()
    {
        var page = new PagedResult<int>([1, 2, 3, 4, 5], pageNumber: 1, pageSize: 2, totalCount: 5);

        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasNextPage);
        Assert.False(page.HasPreviousPage);
    }

    [Fact]
    public void Empty_HasNoItemsAndNoNextPage()
    {
        var page = PagedResult<int>.Empty(pageNumber: 1, pageSize: 10);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalPages);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public void LastPage_HasPreviousButNoNext()
    {
        var page = new PagedResult<int>([9, 10], pageNumber: 5, pageSize: 2, totalCount: 10);

        Assert.True(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
    }
}

public class OrderItemSnapshotTests
{
    [Fact]
    public void Snapshot_KeepsThePriceFromTheMomentOfPurchase()
    {
        var snapshot = new OrderItemSnapshot(Guid.NewGuid(), "Laptop Pro", 2, 999.99m);

        Assert.Equal("Laptop Pro", snapshot.ProductName);
        Assert.Equal(2, snapshot.Quantity);
        Assert.Equal(999.99m, snapshot.UnitPrice);
    }
}
