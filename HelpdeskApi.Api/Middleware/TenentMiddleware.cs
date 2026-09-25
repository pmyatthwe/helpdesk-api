using HelpdeskApi.Application;

namespace HelpdeskApi.Api.Middleware;

// Runs after JWT authentication. Reads the "tenantId" claim from the validated
// token and pushes it into the scoped ICurrentTenantService, which the
// DbContext's query filters read from for the rest of the request.
public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentTenantService currentTenant)
    {
        var tenantClaim = context.User.FindFirst("tenantId")?.Value;

        if (!string.IsNullOrEmpty(tenantClaim) && Guid.TryParse(tenantClaim, out var tenantId))
        {
            currentTenant.SetTenant(tenantId);
        }
        // Endpoints like /api/auth/login and /api/auth/register run before a
        // tenant is known, so they're allowlisted in Program.cs to skip this.

        await _next(context);
    }
}