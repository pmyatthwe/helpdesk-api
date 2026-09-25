using HelpdeskApi.Api.Dtos;
using HelpdeskApi.Domain;
using HelpdeskApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskApi.Api.Controllers;


[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly HelpdeskDbContext _db;

    public UsersController(HelpdeskDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.Users
            .OrderBy(u => u.Email)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.Role,
                u.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }


    [HttpPost("invite")]
    public async Task<IActionResult> InviteUser([FromBody] InviteUserRequest request)
    {
        var emailTaken = await _db.Users.AnyAsync(u => u.Email == request.Email);
        if (emailTaken)
            return Conflict(new { message = "A user with this email already exists in your organization." });

        var temporaryPassword = Guid.NewGuid().ToString("N")[..12];

        var newUser = new User
        {
            Email = request.Email,
            Role = request.Role,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword)
            // TenantId is stamped automatically in DbContext.SaveChangesAsync
        };

        _db.Users.Add(newUser);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            newUser.Id,
            newUser.Email,
            newUser.Role,
            temporaryPassword
        });
    }
}

