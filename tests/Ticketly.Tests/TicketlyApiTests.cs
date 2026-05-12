using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ticketly.Tests;

public class TicketlyApiTests
{
    [Fact]
    public async Task CreateEventSucceedsWithValidData()
    {
        using var factory = new TicketlyApiFactory();
        using var client = factory.CreateClient();

        var eventId = await CreateEventAsync(client);

        Assert.NotEqual(Guid.Empty, eventId);
    }

    [Fact]
    public async Task CreateTicketTypeSucceedsForExistingEvent()
    {
        using var factory = new TicketlyApiFactory();
        using var client = factory.CreateClient();
        var eventId = await CreateEventAsync(client);

        var ticketType = await CreateTicketTypeAsync(client, eventId, totalQuantity: 25);

        Assert.NotEqual(Guid.Empty, ticketType.Id);
        Assert.Equal(eventId, ticketType.EventId);
        Assert.Equal("General Admission", ticketType.Name);
    }

    [Fact]
    public async Task TicketTypeAvailableQuantityInitiallyEqualsTotalQuantity()
    {
        using var factory = new TicketlyApiFactory();
        using var client = factory.CreateClient();
        var eventId = await CreateEventAsync(client);

        var ticketType = await CreateTicketTypeAsync(client, eventId, totalQuantity: 30);

        Assert.Equal(ticketType.TotalQuantity, ticketType.AvailableQuantity);
    }

    [Fact]
    public async Task ReservationSucceedsWhenEnoughTicketsAreAvailable()
    {
        using var factory = new TicketlyApiFactory();
        using var client = factory.CreateClient();
        var ticketType = await CreateEventAndTicketTypeAsync(client, totalQuantity: 10);

        var reservation = await CreateReservationAsync(client, ticketType.Id, quantity: 2);

        Assert.NotEqual(Guid.Empty, reservation.Id);
        Assert.Equal(ticketType.Id, reservation.TicketTypeId);
        Assert.Equal(2, reservation.Quantity);
    }

    [Fact]
    public async Task ReservationReducesAvailableQuantity()
    {
        using var factory = new TicketlyApiFactory();
        using var client = factory.CreateClient();
        var eventId = await CreateEventAsync(client);
        var ticketType = await CreateTicketTypeAsync(client, eventId, totalQuantity: 10);

        await CreateReservationAsync(client, ticketType.Id, quantity: 3);

        var ticketTypes = await client.GetFromJsonAsync<List<TicketTypeResponse>>($"/api/events/{eventId}/ticket-types");
        var updatedTicketType = Assert.Single(ticketTypes!);
        Assert.Equal(7, updatedTicketType.AvailableQuantity);
    }

    [Fact]
    public async Task ReservationFailsWhenNotEnoughTicketsAreAvailable()
    {
        using var factory = new TicketlyApiFactory();
        using var client = factory.CreateClient();
        var ticketType = await CreateEventAndTicketTypeAsync(client, totalQuantity: 2);

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            ticketTypeId = ticketType.Id,
            quantity = 3,
            customerEmail = "customer@example.com"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ReservationFailsWhenQuantityIsZeroOrNegative(int quantity)
    {
        using var factory = new TicketlyApiFactory();
        using var client = factory.CreateClient();
        var ticketType = await CreateEventAndTicketTypeAsync(client, totalQuantity: 10);

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            ticketTypeId = ticketType.Id,
            quantity,
            customerEmail = "customer@example.com"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<Guid> CreateEventAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/events", new
        {
            name = "Test Concert",
            venue = "Main Hall",
            startsAt = DateTimeOffset.UtcNow.AddDays(30)
        });

        await EnsureSuccessAsync(response);
        var createdEvent = await response.Content.ReadFromJsonAsync<EventResponse>();
        return createdEvent!.Id;
    }

    private static async Task<TicketTypeResponse> CreateEventAndTicketTypeAsync(HttpClient client, int totalQuantity)
    {
        var eventId = await CreateEventAsync(client);
        return await CreateTicketTypeAsync(client, eventId, totalQuantity);
    }

    private static async Task<TicketTypeResponse> CreateTicketTypeAsync(
        HttpClient client,
        Guid eventId,
        int totalQuantity)
    {
        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/ticket-types", new
        {
            name = "General Admission",
            price = 25.00m,
            currency = "USD",
            totalQuantity
        });

        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<TicketTypeResponse>())!;
    }

    private static async Task<ReservationResponse> CreateReservationAsync(
        HttpClient client,
        Guid ticketTypeId,
        int quantity)
    {
        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            ticketTypeId,
            quantity,
            customerEmail = "customer@example.com"
        });

        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<ReservationResponse>())!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, responseBody);
    }

    private sealed class TicketlyApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
        }
    }

    private sealed record EventResponse(Guid Id, string Name, string Venue, DateTimeOffset StartsAt, DateTimeOffset CreatedAt);

    private sealed record TicketTypeResponse(
        Guid Id,
        Guid EventId,
        string Name,
        decimal Price,
        string Currency,
        int TotalQuantity,
        int AvailableQuantity,
        DateTimeOffset CreatedAt);

    private sealed record ReservationResponse(
        Guid Id,
        Guid TicketTypeId,
        int Quantity,
        string CustomerEmail,
        DateTimeOffset CreatedAt);
}
