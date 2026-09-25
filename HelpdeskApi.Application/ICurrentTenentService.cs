namespace HelpdeskApi.Application;

// Resolved once per request by TenantMiddleware from the JWT's "tenantId" claim.
// Injected into the DbContext so every query is automatically scoped.
public interface ICurrentTenantService
{
    Guid? TenantId { get; }
    void SetTenant(Guid tenantId);
}

public class CurrentTenantService : ICurrentTenantService
{
    public Guid? TenantId { get; private set; }
    public void SetTenant(Guid tenantId) => TenantId = tenantId;
}