using System;
using System.Collections.Concurrent;
using SagaSales.Sales.Domain;

namespace SagaSales.Sales.Infrastructure;

public static class CustomerCreditStore
{
    private static readonly ConcurrentDictionary<Guid, CustomerCredit> _credits = new();

    public static CustomerCredit? Get(Guid customerId) => _credits.TryGetValue(customerId, out var credit) ? credit : null;

    public static void AddOrUpdate(Guid customerId, decimal limit, decimal available)
    {
        var credit = new CustomerCredit { CustomerId = customerId, CreditLimit = limit, Available = available };
        _credits[customerId] = credit;
    }
}
