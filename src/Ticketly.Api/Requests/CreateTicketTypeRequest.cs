namespace Ticketly.Api.Requests;

public record CreateTicketTypeRequest(string Name, decimal Price, string Currency, int TotalQuantity);

