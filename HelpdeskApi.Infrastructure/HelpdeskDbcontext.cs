using HelpdeskApi.Application;
using HelpdeskApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskApi.Infrastructure;

public class HelpdeskDbContext : DbContext
{
    private readonly ICurrentTenantService _currentTenant;

    public HelpdeskDbContext(DbContextOptions<HelpdeskDbContext> options, ICurrentTenantService currentTenant)
        : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Global query filters: every read is automatically scoped to the
        // current tenant. This is the core of the isolation strategy: callers
        // never need to remember to add "WHERE TenantId = ..." themselves.
        modelBuilder.Entity<User>()
            .HasQueryFilter(u => u.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<Ticket>()
            .HasQueryFilter(t => t.TenantId == _currentTenant.TenantId);

        modelBuilder.Entity<Comment>()
            .HasQueryFilter(c => c.Ticket.TenantId == _currentTenant.TenantId);

        // --- Relationships that need explicit configuration
        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.CreatedByUser)
            .WithMany(u => u.CreatedTickets)
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.AssignedAgent)
            .WithMany(u => u.AssignedTickets)
            .HasForeignKey(t => t.AssignedAgentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<User>()
            .HasIndex(u => new { u.TenantId, u.Email })
            .IsUnique();
    }

    // Ensures every new row gets stamped with the current tenant automatically,
    // so a developer can't accidentally insert a row for the wrong tenant.
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.TenantId;
        if (tenantId is not null)
        {
            foreach (var entry in ChangeTracker.Entries<Ticket>())
                if (entry.State == EntityState.Added)
                    entry.Entity.TenantId = tenantId.Value;

            foreach (var entry in ChangeTracker.Entries<User>())
                if (entry.State == EntityState.Added)
                    entry.Entity.TenantId = tenantId.Value;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}