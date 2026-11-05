namespace SagaSales.Shared.DTOs;

/// <summary>
/// Reserving stock always happens in one branch (E-13, D16). When it is not given, the store's
/// default branch is used, which keeps callers from before E-13 working unchanged.
/// </summary>
public class ReserveStockRequest
{
    public int Quantity { get; set; }

    public Guid? BranchId { get; set; }
}
