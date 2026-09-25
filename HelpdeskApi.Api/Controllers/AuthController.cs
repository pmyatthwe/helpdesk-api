using HelpdeskApi.Api.Dtos;
using HelpdeskApi.Application;
using HelpdeskApi.Domain;
using HelpdeskApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskApi.Api.Controllers;

// These two endpoints run BEFORE a tenant is known, so they're the only
// places in the app that deliberately bypass the DbContext's global query
// filters (via IgnoreQueryFilters) or don't rely on them at all.
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly HelpdeskDbContext _db;
    private readonly ITokenService _tokenService;

    public AuthController(HelpdeskDbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    // Creates a brand-new tenant plus its first Admin user in one step.
    // This is how a new company signs up for the product.
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var subdomainTaken = await _db.Tenants
            .IgnoreQueryFilters() // Tenants has no filter anyway, but explicit for clarity
            .AnyAsync(t => t.Subdomain == request.Subdomain);

        if (subdomainTaken)
            return Conflict(new { message = "Subdomain is already in use." });

        var tenant = new Tenant
        {
            Name = request.CompanyName,
            Subdomain = request.Subdomain
        };

        var adminUser = new User
        {
            Email = request.AdminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Admin,
            Tenant = tenant // EF Core sets TenantId automatically via this navigation
        };

        _db.Tenants.Add(tenant);
        _db.Users.Add(adminUser);
        await _db.SaveChangesAsync();

        var token = _tokenService.GenerateToken(adminUser);
        return Ok(new AuthResponse(token, adminUser.Email, adminUser.Role.ToString(), tenant.Id));
    }

    // Logs a user into their existing tenant. Subdomain + email together
    // identify the user, since email is only unique WITHIN a tenant
    // (see the composite unique index in HelpdeskDbContext).
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        // IgnoreQueryFilters is required here: at this point in the request,
        // ICurrentTenantService.TenantId is still null (no JWT has been
        // presented yet), so the normal global filter would match nothing.
        var user = await _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.Tenant.Subdomain == request.Subdomain);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid credentials." });

        var token = _tokenService.GenerateToken(user);
        return Ok(new AuthResponse(token, user.Email, user.Role.ToString(), user.TenantId));
    }
}