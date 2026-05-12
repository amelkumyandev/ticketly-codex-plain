using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Ticketly.Api.Data;
using Ticketly.Api.Models;
using Ticketly.Api.Requests;

var builder = WebApplication.CreateBuilder(args);
var jwtSettings = GetJwtSettings(builder.Configuration);

builder.Services.AddDbContext<TicketlyDbContext>(options =>
{
    if (builder.Environment.IsEnvironment("Testing"))
    {
        options.UseInMemoryDatabase(builder.Configuration["TestingDatabaseName"] ?? "ticketly-tests");
        return;
    }

    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("CustomerOrAdmin", policy => policy.RequireRole("Customer", "Admin"));
});
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .WithName("Health");

app.MapPost("/api/auth/register", async (RegisterRequest request, TicketlyDbContext dbContext) =>
{
    var validationError = ValidateAuthInput(request.Email, request.Password);
    if (validationError is not null)
    {
        return Results.BadRequest(new { error = validationError });
    }

    var role = NormalizeRole(request.Role);
    if (role is null)
    {
        return Results.BadRequest(new { error = "Role must be Admin or Customer." });
    }

    var email = request.Email.Trim().ToLowerInvariant();
    var emailExists = await dbContext.Users.AnyAsync(user => user.Email.ToLower() == email);
    if (emailExists)
    {
        return Results.BadRequest(new { error = "Email is already registered." });
    }

    var user = new ApplicationUser
    {
        Email = email,
        PasswordHash = HashPassword(request.Password),
        Role = role,
        CreatedAt = DateTimeOffset.UtcNow
    };

    dbContext.Users.Add(user);
    await dbContext.SaveChangesAsync();

    return Results.Created($"/api/users/{user.Id}", ToUserResponse(user));
});

app.MapPost("/api/auth/login", async (LoginRequest request, TicketlyDbContext dbContext) =>
{
    var validationError = ValidateAuthInput(request.Email, request.Password);
    if (validationError is not null)
    {
        return Results.BadRequest(new { error = validationError });
    }

    var email = request.Email.Trim().ToLowerInvariant();
    var user = await dbContext.Users.SingleOrDefaultAsync(applicationUser => applicationUser.Email.ToLower() == email);
    if (user is null || !VerifyPassword(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new
    {
        accessToken = CreateAccessToken(user, jwtSettings),
        tokenType = "Bearer",
        expiresInMinutes = jwtSettings.ExpiresMinutes
    });
});

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
}).RequireAuthorization("AdminOnly");

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
}).RequireAuthorization("AdminOnly");

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
}).RequireAuthorization("CustomerOrAdmin");

app.MapGet("/api/reservations/{id:guid}", async (Guid id, TicketlyDbContext dbContext) =>
{
    var reservation = await dbContext.Reservations.FindAsync(id);

    return reservation is null
        ? Results.NotFound()
        : Results.Ok(ToReservationResponse(reservation));
}).RequireAuthorization("CustomerOrAdmin");

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

static object ToUserResponse(ApplicationUser user) => new
{
    user.Id,
    user.Email,
    user.Role,
    user.CreatedAt
};

static JwtSettings GetJwtSettings(IConfiguration configuration)
{
    var issuer = configuration["Jwt:Issuer"] ?? configuration["Jwt__Issuer"];
    var audience = configuration["Jwt:Audience"] ?? configuration["Jwt__Audience"];
    var signingKey = configuration["Jwt:SigningKey"] ?? configuration["Jwt__SigningKey"];
    var expiresMinutesValue = configuration["Jwt:ExpiresMinutes"] ?? configuration["Jwt__ExpiresMinutes"];

    if (string.IsNullOrWhiteSpace(issuer)
        || string.IsNullOrWhiteSpace(audience)
        || string.IsNullOrWhiteSpace(signingKey))
    {
        throw new InvalidOperationException("JWT configuration requires issuer, audience, and signing key.");
    }

    if (Encoding.UTF8.GetByteCount(signingKey) < 32)
    {
        throw new InvalidOperationException("JWT signing key must be at least 32 bytes.");
    }

    var expiresMinutes = int.TryParse(expiresMinutesValue, out var parsedExpiresMinutes) && parsedExpiresMinutes > 0
        ? parsedExpiresMinutes
        : 60;

    return new JwtSettings(issuer, audience, signingKey, expiresMinutes);
}

static string? ValidateAuthInput(string email, string password)
{
    if (string.IsNullOrWhiteSpace(email))
    {
        return "Email is required.";
    }

    if (string.IsNullOrWhiteSpace(password))
    {
        return "Password is required.";
    }

    return null;
}

static string? NormalizeRole(string role)
{
    if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
    {
        return "Admin";
    }

    if (role.Equals("Customer", StringComparison.OrdinalIgnoreCase))
    {
        return "Customer";
    }

    return null;
}

static string HashPassword(string password)
{
    const int iterations = 100_000;
    var salt = RandomNumberGenerator.GetBytes(16);
    var hash = Rfc2898DeriveBytes.Pbkdf2(
        password,
        salt,
        iterations,
        HashAlgorithmName.SHA256,
        32);

    return $"pbkdf2-sha256${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
}

static bool VerifyPassword(string password, string passwordHash)
{
    var parts = passwordHash.Split('$');
    if (parts.Length != 4
        || parts[0] != "pbkdf2-sha256"
        || !int.TryParse(parts[1], out var iterations))
    {
        return false;
    }

    var salt = Convert.FromBase64String(parts[2]);
    var expectedHash = Convert.FromBase64String(parts[3]);
    var actualHash = Rfc2898DeriveBytes.Pbkdf2(
        password,
        salt,
        iterations,
        HashAlgorithmName.SHA256,
        expectedHash.Length);

    return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
}

static string CreateAccessToken(ApplicationUser user, JwtSettings jwtSettings)
{
    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role)
    };

    var signingCredentials = new SigningCredentials(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
        SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: jwtSettings.Issuer,
        audience: jwtSettings.Audience,
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(jwtSettings.ExpiresMinutes),
        signingCredentials: signingCredentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
}

internal sealed record JwtSettings(string Issuer, string Audience, string SigningKey, int ExpiresMinutes);

public partial class Program;
