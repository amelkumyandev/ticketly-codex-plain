using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Ticketly.Tests;

public class TicketlyApiTests
{
    [Fact]
    public async Task RegisterSucceedsWithValidData()
    {
        using var factory = new TicketlyApiFactory();
        using var client = factory.CreateClient();

        var response = await RegisterAsync(client, "new-admin@example.com", "Admin");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotEqual(Guid.Empty, user!.Id);
        Assert.Equal("new-admin@example.com", user.Email);
        Assert.Equal("Admin", user.Role);
    }

    [Fact]
    public async Task RegisterFailsForDuplicateEmail()
    {
        using var factory = new TicketlyApiFactory();
        using var client = factory.CreateClient();

        await EnsureSuccessAsync(await RegisterAsync(client, "duplicate@example.com", "Customer"));
        var duplicateResponse = await RegisterAsync(client, "DUPLICATE@example.com", "Customer");

        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task LoginSucceedsWithValidCredentials()
    {
        using var factory = new TicketlyApiFactory();
        using var client = factory.CreateClient();
        await EnsureSuccessAsync(await RegisterAsync(client, "login@example.com", "Customer"));

        var response = await LoginAsync(client, "login@example.com", TestPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(login!.AccessToken));
        Assert.Equal("Bearer", login.TokenType);
    }

    [Fact]
    public async Task LoginFailsWithInvalidCredentials()
    {
        using var factory = new TicketlyApiFactory();
        using var client = factory.CreateClient();
        await EnsureSuccessAsync(await RegisterAsync(client, "bad-login@example.com", "Customer"));

        var response = await LoginAsync(client, "bad-login@example.com", "wrong-password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnlyEndpointRejectsAnonymousUsers()
    {
        using var factory = new TicketlyApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/events", NewEventRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnlyEndpointRejectsCustomerRole()
    {
        using var factory = new TicketlyApiFactory();
        using var client = factory.CreateClient();
        await AuthorizeAsync(client, "Customer");

        var response = await client.PostAsJsonAsync("/api/events", NewEventRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnlyEndpointAcceptsAdminRole()
    {
        using var factory = new TicketlyApiFactory();
        using var client = factory.CreateClient();
        await AuthorizeAsync(client, "Admin");

        var response = await client.PostAsJsonAsync("/api/events", NewEventRequest());

        await EnsureSuccessAsync(response);
        var createdEvent = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotEqual(Guid.Empty, createdEvent!.Id);
    }

    [Fact]
    public async Task ReservationEndpointRejectsAnonymousUsers()
    {
        using var factory = new TicketlyApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            ticketTypeId = Guid.NewGuid(),
            quantity = 1,
            customerEmail = "customer@example.com"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReservationEndpointAcceptsCustomerRole()
    {
        using var factory = new TicketlyApiFactory();
        using var client = factory.CreateClient();
        var ticketType = await CreateEventAndTicketTypeAsync(client, totalQuantity: 10);
        await AuthorizeAsync(client, "Customer");

        var reservation = await CreateReservationAsync(client, ticketType.Id, quantity: 2);

        Assert.NotEqual(Guid.Empty, reservation.Id);
        Assert.Equal(ticketType.Id, reservation.TicketTypeId);
        Assert.Equal(2, reservation.Quantity);
    }

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
        await AuthorizeAsync(client, "Customer");

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
        await AuthorizeAsync(client, "Customer");

        await CreateReservationAsync(client, ticketType.Id, quantity: 3);
        client.DefaultRequestHeaders.Authorization = null;

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
        await AuthorizeAsync(client, "Customer");

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
        await AuthorizeAsync(client, "Customer");

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            ticketTypeId = ticketType.Id,
            quantity,
            customerEmail = "customer@example.com"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private const string TestPassword = "CorrectHorseBatteryStaple1!";

    private static object NewEventRequest() => new
    {
        name = "Test Concert",
        venue = "Main Hall",
        startsAt = DateTimeOffset.UtcNow.AddDays(30)
    };

    private static async Task<Guid> CreateEventAsync(HttpClient client)
    {
        await AuthorizeAsync(client, "Admin");
        var response = await client.PostAsJsonAsync("/api/events", NewEventRequest());

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
        await AuthorizeAsync(client, "Admin");
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

    private static async Task AuthorizeAsync(HttpClient client, string role)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";
        await EnsureSuccessAsync(await RegisterAsync(client, email, role));
        var loginResponse = await LoginAsync(client, email, TestPassword);
        await EnsureSuccessAsync(loginResponse);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, string role)
    {
        return client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            role
        });
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password)
    {
        return client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password
        });
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
            builder.ConfigureAppConfiguration(configurationBuilder =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TestingDatabaseName"] = $"ticketly-tests-{Guid.NewGuid():N}",
                    ["Jwt:Issuer"] = "ticketly-tests",
                    ["Jwt:Audience"] = "ticketly-api-tests",
                    ["Jwt:SigningKey"] = "ticketly-test-signing-key-with-at-least-32-bytes",
                    ["Jwt:ExpiresMinutes"] = "60"
                });
            });
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

    private sealed record UserResponse(Guid Id, string Email, string Role, DateTimeOffset CreatedAt);

    private sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresInMinutes);
}
