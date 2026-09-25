namespace HelpdeskApi.Api.Dtos;

public record RegisterRequest(string CompanyName, string Subdomain, string AdminEmail, string Password);
public record LoginRequest(string Subdomain, string Email, string Password);
public record AuthResponse(string Token, string Email, string Role, Guid TenantId);