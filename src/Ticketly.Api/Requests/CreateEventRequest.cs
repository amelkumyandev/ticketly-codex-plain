namespace Ticketly.Api.Requests;

public record CreateEventRequest(string Name, string Venue, DateTimeOffset StartsAt);

