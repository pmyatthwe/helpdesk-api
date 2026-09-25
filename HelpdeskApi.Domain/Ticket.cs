namespace HelpdeskApi.Domain;

public enum TicketStatus
{
    Open,
    InProgress,
    Closed
}

public enum TicketPriority
{
    Low,
    Medium,
    High,
    Urgent
}

public class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = default!;
    public Guid? AssignedAgentId { get; set; }
    public User? AssignedAgent { get; set; }
    public string Subject { get; set; } = default!;
    public string Description { get; set; } = default!;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
}

public class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = default!;
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public string Body { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}