namespace Ticketly.Api.Models;

public class Reservation
{
    public Guid Id { get; set; }

    public Guid TicketTypeId { get; set; }

    public int Quantity { get; set; }

    public string CustomerEmail { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

