using HelpdeskApi.Domain;

namespace HelpdeskApi.Api.Dtos;

public record CreateTicketRequest(string Subject, string Description, TicketPriority Priority);
public record UpdateTicketRequest(TicketStatus? Status, Guid? AssignedAgentId);