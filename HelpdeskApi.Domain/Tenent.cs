namespace HelpdeskApi.Domain;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string Subdomain { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}