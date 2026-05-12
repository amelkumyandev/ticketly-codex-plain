namespace Ticketly.Api.Requests;

public record CreateReservationRequest(Guid TicketTypeId, int Quantity, string CustomerEmail);
