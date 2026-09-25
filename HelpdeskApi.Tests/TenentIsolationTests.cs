using HelpdeskApi.Application;
using HelpdeskApi.Domain;
using HelpdeskApi.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HelpdeskApi.Tests;

// These tests prove the core design claim of the project: a user from one
// tenant can never see another tenant's data, even via a direct query,
// because the isolation is enforced by EF Core's global query filter rather
// than by remembering to add "WHERE TenantId = ..." in every call site.
public class TenantIsolationTests
{
    private static HelpdeskDbContext CreateContext(CurrentTenantService currentTenant, string dbName)
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new HelpdeskDbContext(options, currentTenant);
    }

    [Fact]
    public async Task User_Cannot_See_Tickets_From_Another_Tenant()
    {
        var dbName = Guid.NewGuid().ToString();
        var seedTenant = new CurrentTenantService();

        var tenantA = new Tenant { Name = "Company A", Subdomain = "company-a" };
        var tenantB = new Tenant { Name = "Company B", Subdomain = "company-b" };

        var userA = new User { Tenant = tenantA, Email = "admin@a.com", PasswordHash = "x", Role = UserRole.Admin };
        var userB = new User { Tenant = tenantB, Email = "admin@b.com", PasswordHash = "x", Role = UserRole.Admin };

        await using (var seedContext = CreateContext(seedTenant, dbName))
        {
            seedContext.Tenants.AddRange(tenantA, tenantB);
            seedContext.Users.AddRange(userA, userB);
            await seedContext.SaveChangesAsync();

            seedContext.Tickets.AddRange(
                new Ticket { TenantId = tenantA.Id, CreatedByUserId = userA.Id, Subject = "A's ticket", Description = "..." },
                new Ticket { TenantId = tenantB.Id, CreatedByUserId = userB.Id, Subject = "B's ticket", Description = "..." }
            );
            await seedContext.SaveChangesAsync();
        }

        var tenantAScope = new CurrentTenantService();
        tenantAScope.SetTenant(tenantA.Id);

        await using var scopedContext = CreateContext(tenantAScope, dbName);
        var visibleTickets = await scopedContext.Tickets.ToListAsync();

        Assert.Single(visibleTickets);
        Assert.Equal("A's ticket", visibleTickets[0].Subject);
    }

    [Fact]
    public async Task New_Ticket_Is_Automatically_Stamped_With_Current_Tenant()
    {
        var dbName = Guid.NewGuid().ToString();
        var seedTenant = new CurrentTenantService();

        var tenant = new Tenant { Name = "Company A", Subdomain = "company-a" };
        var user = new User { Tenant = tenant, Email = "admin@a.com", PasswordHash = "x", Role = UserRole.Admin };

        await using (var seedContext = CreateContext(seedTenant, dbName))
        {
            seedContext.Tenants.Add(tenant);
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync();
        }

        var tenantScope = new CurrentTenantService();
        tenantScope.SetTenant(tenant.Id);

        await using var scopedContext = CreateContext(tenantScope, dbName);
        var newTicket = new Ticket
        {
            CreatedByUserId = user.Id,
            Subject = "New ticket",
            Description = "..."
            // TenantId deliberately left unset here
        };

        scopedContext.Tickets.Add(newTicket);
        await scopedContext.SaveChangesAsync();

        Assert.Equal(tenant.Id, newTicket.TenantId);
    }

    [Fact]
    public async Task Comments_Are_Also_Scoped_Through_Their_Parent_Ticket()
    {
        var dbName = Guid.NewGuid().ToString();
        var seedTenant = new CurrentTenantService();

        var tenantA = new Tenant { Name = "Company A", Subdomain = "company-a" };
        var tenantB = new Tenant { Name = "Company B", Subdomain = "company-b" };
        var userA = new User { Tenant = tenantA, Email = "a@a.com", PasswordHash = "x", Role = UserRole.Admin };
        var userB = new User { Tenant = tenantB, Email = "b@b.com", PasswordHash = "x", Role = UserRole.Admin };

        Ticket ticketA, ticketB;

        await using (var seedContext = CreateContext(seedTenant, dbName))
        {
            seedContext.Tenants.AddRange(tenantA, tenantB);
            seedContext.Users.AddRange(userA, userB);
            await seedContext.SaveChangesAsync();

            ticketA = new Ticket { TenantId = tenantA.Id, CreatedByUserId = userA.Id, Subject = "A", Description = "..." };
            ticketB = new Ticket { TenantId = tenantB.Id, CreatedByUserId = userB.Id, Subject = "B", Description = "..." };
            seedContext.Tickets.AddRange(ticketA, ticketB);
            await seedContext.SaveChangesAsync();

            seedContext.Comments.AddRange(
                new Comment { TicketId = ticketA.Id, UserId = userA.Id, Body = "Comment on A" },
                new Comment { TicketId = ticketB.Id, UserId = userB.Id, Body = "Comment on B" }
            );
            await seedContext.SaveChangesAsync();
        }

        var tenantAScope = new CurrentTenantService();
        tenantAScope.SetTenant(tenantA.Id);

        await using var scopedContext = CreateContext(tenantAScope, dbName);
        var visibleComments = await scopedContext.Comments.ToListAsync();

        Assert.Single(visibleComments);
        Assert.Equal("Comment on A", visibleComments[0].Body);
    }
}