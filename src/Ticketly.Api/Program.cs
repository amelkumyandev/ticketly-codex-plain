using Microsoft.EntityFrameworkCore;
using Ticketly.Api.Data;
using Ticketly.Api.Models;
using Ticketly.Api.Requests;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TicketlyDbContext>(options =>
{
    if (builder.Environment.IsEnvironment("Testing"))
    {
        options.UseInMemoryDatabase("ticketly-tests");
        return;
    }

    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .WithName("Health");

app.MapPost("/api/events", async (CreateEventRequest request, TicketlyDbContext dbContext) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Event name is required." });
    }

    var ticketEvent = new Event
    {
        Name = request.Name.Trim(),
        Venue = request.Venue?.Trim() ?? string.Empty,
        StartsAt = request.StartsAt,
        CreatedAt = DateTimeOffset.UtcNow
    };

    dbContext.Events.Add(ticketEvent);
    await dbContext.SaveChangesAsync();

    return Results.Created($"/api/events/{ticketEvent.Id}", ToEventResponse(ticketEvent));
});

app.MapGet("/api/events", async (TicketlyDbContext dbContext) =>
{
    var events = await dbContext.Events
        .OrderBy(ticketEvent => ticketEvent.StartsAt)
        .Select(ticketEvent => new
        {
            ticketEvent.Id,
            ticketEvent.Name,
            ticketEvent.Venue,
            ticketEvent.StartsAt,
            ticketEvent.CreatedAt
        })
        .ToListAsync();

    return Results.Ok(events);
});

app.MapGet("/api/events/{id:guid}", async (Guid id, TicketlyDbContext dbContext) =>
{
    var ticketEvent = await dbContext.Events.FindAsync(id);

    return ticketEvent is null
        ? Results.NotFound()
        : Results.Ok(ToEventResponse(ticketEvent));
});

app.MapPost("/api/events/{eventId:guid}/ticket-types", async (
    Guid eventId,
    CreateTicketTypeRequest request,
    TicketlyDbContext dbContext) =>
{
    var ticketEventExists = await dbContext.Events.AnyAsync(ticketEvent => ticketEvent.Id == eventId);
    if (!ticketEventExists)
    {
        return Results.NotFound(new { error = "Event was not found." });
    }

    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Ticket type name is required." });
    }

    if (request.Price < 0)
    {
        return Results.BadRequest(new { error = "Ticket type price cannot be negative." });
    }

    if (request.TotalQuantity <= 0)
    {
        return Results.BadRequest(new { error = "Ticket type total quantity must be greater than zero." });
    }

    if (string.IsNullOrWhiteSpace(request.Currency))
    {
        return Results.BadRequest(new { error = "Ticket type currency is required." });
    }

    var ticketType = new TicketType
    {
        EventId = eventId,
        Name = request.Name.Trim(),
        Price = request.Price,
        Currency = request.Currency.Trim().ToUpperInvariant(),
        TotalQuantity = request.TotalQuantity,
        AvailableQuantity = request.TotalQuantity,
        CreatedAt = DateTimeOffset.UtcNow
    };

    dbContext.TicketTypes.Add(ticketType);
    await dbContext.SaveChangesAsync();

    return Results.Created($"/api/events/{eventId}/ticket-types/{ticketType.Id}", ToTicketTypeResponse(ticketType));
});

app.MapGet("/api/events/{eventId:guid}/ticket-types", async (Guid eventId, TicketlyDbContext dbContext) =>
{
    var ticketEventExists = await dbContext.Events.AnyAsync(ticketEvent => ticketEvent.Id == eventId);
    if (!ticketEventExists)
    {
        return Results.NotFound(new { error = "Event was not found." });
    }

    var ticketTypes = await dbContext.TicketTypes
        .Where(ticketType => ticketType.EventId == eventId)
        .OrderBy(ticketType => ticketType.Name)
        .Select(ticketType => new
        {
            ticketType.Id,
            ticketType.EventId,
            ticketType.Name,
            ticketType.Price,
            ticketType.Currency,
            ticketType.TotalQuantity,
            ticketType.AvailableQuantity,
            ticketType.CreatedAt
        })
        .ToListAsync();

    return Results.Ok(ticketTypes);
});

app.MapPost("/api/reservations", async (CreateReservationRequest request, TicketlyDbContext dbContext) =>
{
    if (request.Quantity <= 0)
    {
        return Results.BadRequest(new { error = "Reservation quantity must be greater than zero." });
    }

    if (string.IsNullOrWhiteSpace(request.CustomerEmail))
    {
        return Results.BadRequest(new { error = "Customer email is required." });
    }

    var ticketType = await dbContext.TicketTypes.FindAsync(request.TicketTypeId);
    if (ticketType is null)
    {
        return Results.NotFound(new { error = "Ticket type was not found." });
    }

    if (ticketType.AvailableQuantity < request.Quantity)
    {
        return Results.BadRequest(new { error = "Not enough tickets are available." });
    }

    ticketType.AvailableQuantity -= request.Quantity;

    var reservation = new Reservation
    {
        TicketTypeId = request.TicketTypeId,
        Quantity = request.Quantity,
        CustomerEmail = request.CustomerEmail.Trim(),
        CreatedAt = DateTimeOffset.UtcNow
    };

    dbContext.Reservations.Add(reservation);
    await dbContext.SaveChangesAsync();

    return Results.Created($"/api/reservations/{reservation.Id}", ToReservationResponse(reservation));
});

app.MapGet("/api/reservations/{id:guid}", async (Guid id, TicketlyDbContext dbContext) =>
{
    var reservation = await dbContext.Reservations.FindAsync(id);

    return reservation is null
        ? Results.NotFound()
        : Results.Ok(ToReservationResponse(reservation));
});

app.Run();

static object ToEventResponse(Event ticketEvent) => new
{
    ticketEvent.Id,
    ticketEvent.Name,
    ticketEvent.Venue,
    ticketEvent.StartsAt,
    ticketEvent.CreatedAt
};

static object ToTicketTypeResponse(TicketType ticketType) => new
{
    ticketType.Id,
    ticketType.EventId,
    ticketType.Name,
    ticketType.Price,
    ticketType.Currency,
    ticketType.TotalQuantity,
    ticketType.AvailableQuantity,
    ticketType.CreatedAt
};

static object ToReservationResponse(Reservation reservation) => new
{
    reservation.Id,
    reservation.TicketTypeId,
    reservation.Quantity,
    reservation.CustomerEmail,
    reservation.CreatedAt
};

public partial class Program;
