namespace Ticketly.Api.Requests;

public record RegisterRequest(string Email, string Password, string Role);
