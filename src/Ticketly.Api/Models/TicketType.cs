namespace Ticketly.Api.Models;

public class TicketType
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string Currency { get; set; } = "USD";

    public int TotalQuantity { get; set; }

    public int AvailableQuantity { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

