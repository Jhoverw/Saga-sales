namespace SagaSales.Shared.Enums;

public enum OrderStatus
{
    Pending = 1,
    Confirmed = 2,
    Processing = 3,
    Shipped = 4,
    Delivered = 5,
    Cancelled = 6,
    Refunded = 7,
}

public enum PaymentStatus
{
    Pending = 1,
    Authorized = 2,
    Captured = 3,
    Failed = 4,
    Refunded = 5,
    PartiallyRefunded = 6,
}

public enum PaymentMethod
{
    CreditCard = 1,
    DebitCard = 2,
    PayPal = 3,
    BankTransfer = 4,
    CashOnDelivery = 5,
    Stripe = 6,
}

public enum ProductStatus
{
    Draft = 1,
    Active = 2,
    Inactive = 3,
    OutOfStock = 4,
    Discontinued = 5,
}

public enum CustomerStatus
{
    Active = 1,
    Inactive = 2,
    Suspended = 3,
    Deleted = 4,
}

public enum TenantStatus
{
    Active = 1,
    Trial = 2,
    Suspended = 3,
    Cancelled = 4,
}

public enum NotificationType
{
    Email = 1,
    SMS = 2,
    Push = 3,
    InApp = 4,
}

public enum NotificationStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3,
    Delivered = 4,
    Read = 5,
}

public enum Currency
{
    USD = 840,
    EUR = 978,
    GBP = 826,
    MXN = 484,
    COP = 170,
    ARS = 32,
    CLP = 152,
    PEN = 604,
}

/// <summary>Who the buyer is for pricing purposes (decision D5).</summary>
public enum CustomerType
{
    Retail = 1,
    Wholesale = 2,
}

/// <summary>Which price was applied to a quote or an order line (FR-007b).</summary>
public enum PriceSource
{
    Base = 1,
    List = 2,
}

/// <summary>How money was received when there is no online gateway (E-04a). Values are the stored method ids.</summary>
public enum ManualPaymentMethod
{
    /// <summary>Card, recorded without a gateway (H-10). Stored as credit card (1).</summary>
    Card = 1,
    Transfer = 4,
    Cash = 5,
    Deposit = 9,
}