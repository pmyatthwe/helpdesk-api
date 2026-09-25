using HelpdeskApi.Domain;

namespace HelpdeskApi.Api.Dtos;

public record InviteUserRequest(string Email, UserRole Role);