using System;

namespace SagaSales.Sales.Domain;

public class CustomerCredit
{
    public Guid CustomerId { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal Available { get; set; }
}
