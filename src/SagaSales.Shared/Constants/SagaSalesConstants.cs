namespace SagaSales.Shared.Constants;

public static class SagaSalesConstants
{
    public const string ApplicationName = "SagaSales";
    public const string DefaultCulture = "es-ES";
    public const string SupportedCultures = "es-ES,en-US";
    
    public static class Headers
    {
        public const string TenantId = "X-Tenant-Id";
        public const string CorrelationId = "X-Correlation-Id";
        public const string RequestId = "X-Request-Id";
    }

    public static class Claims
    {
        public const string TenantId = "tenant_id";
        public const string UserId = "sub";
        public const string Email = "email";
        public const string Roles = "role";
        public const string Permissions = "permissions";
    }

    public static class Policies
    {
        public const string RequireAdmin = "RequireAdmin";
        public const string RequireManager = "RequireManager";
        public const string RequireSales = "RequireSales";
        public const string RequireCustomer = "RequireCustomer";
        public const string RequireTenantAccess = "RequireTenantAccess";
    }

    public static class RabbitMQ
    {
        public const string ExchangeName = "saga-sales";
        public const string DeadLetterExchange = "saga-sales.dlx";
        public const string DefaultQueuePrefix = "saga-sales.";
    }

    public static class Cache
    {
        public const string ProductCachePrefix = "product:";
        public const string CustomerCachePrefix = "customer:";
        public const string TenantCachePrefix = "tenant:";
        public const int DefaultExpirationMinutes = 30;
    }
}