# Ticketly

Ticketly is a small .NET 10 Web API for event ticket reservation.

## Project Structure

- `Ticketly.sln` - .NET solution
- `src/Ticketly.Api` - ASP.NET Core Web API
- `tests/Ticketly.Tests` - xUnit test project
- `docker-compose.yml` - API and PostgreSQL services
- `TOKEN_BURN.md` - token-burn tracking notes
- `scripts/estimate-token-burn.ps1` - local token estimate script

## Requirements

- .NET 10 SDK
- Docker Desktop
- PowerShell

## Build

```powershell
dotnet build
```

## Test

```powershell
dotnet test
```

## Test Coverage

Generate coverage with:

```powershell
dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults
```

Coverage files are written under `TestResults`.

Generate an OpenCover report for SonarQube with:

```powershell
.\scripts\run-tests-with-coverage.ps1
```

The OpenCover report is written to `coverage/coverage.opencover.xml`.

## Run Locally

```powershell
dotnet run --project src/Ticketly.Api
```

Health check:

```powershell
Invoke-RestMethod http://localhost:5165/health
```

## JWT Configuration

Ticketly uses JWT bearer authentication for protected write and reservation endpoints.

Configuration keys can come from `appsettings.json`, user secrets, or environment variables:

```powershell
$env:Jwt__Issuer = "ticketly-local"
$env:Jwt__Audience = "ticketly-api"
$env:Jwt__SigningKey = "replace-with-a-long-random-secret-at-least-32-bytes"
$env:Jwt__ExpiresMinutes = "60"
```

The repository includes local demo JWT settings so the API can run for development. Replace the signing key before using the API outside a local demo.

## API Endpoints

Public:

- `GET /health`
- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/events`
- `GET /api/events/{id}`
- `GET /api/events/{eventId}/ticket-types`

Admin only:

- `POST /api/events`
- `POST /api/events/{eventId}/ticket-types`

Customer or Admin:

- `POST /api/reservations`
- `GET /api/reservations/{id}`

## Authentication

Register a user:

```powershell
$admin = Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5165/api/auth/register `
  -ContentType "application/json" `
  -Body '{"email":"admin@example.com","password":"Passw0rd!","role":"Admin"}'
```

Supported roles are `Admin` and `Customer`.

Login:

```powershell
$login = Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5165/api/auth/login `
  -ContentType "application/json" `
  -Body '{"email":"admin@example.com","password":"Passw0rd!"}'

$headers = @{ Authorization = "Bearer $($login.accessToken)" }
```

Call a protected endpoint:

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5165/api/events `
  -Headers $headers `
  -ContentType "application/json" `
  -Body '{"name":"Launch Night","venue":"Main Hall","startsAt":"2026-06-01T19:00:00Z"}'
```

Example Admin flow:

```powershell
$adminLogin = Invoke-RestMethod -Method Post -Uri http://localhost:5165/api/auth/login -ContentType "application/json" -Body '{"email":"admin@example.com","password":"Passw0rd!"}'
$adminHeaders = @{ Authorization = "Bearer $($adminLogin.accessToken)" }
$event = Invoke-RestMethod -Method Post -Uri http://localhost:5165/api/events -Headers $adminHeaders -ContentType "application/json" -Body '{"name":"Launch Night","venue":"Main Hall","startsAt":"2026-06-01T19:00:00Z"}'
$ticketType = Invoke-RestMethod -Method Post -Uri "http://localhost:5165/api/events/$($event.id)/ticket-types" -Headers $adminHeaders -ContentType "application/json" -Body '{"name":"General Admission","price":25,"currency":"USD","totalQuantity":100}'
```

Example Customer flow:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5165/api/auth/register -ContentType "application/json" -Body '{"email":"customer@example.com","password":"Passw0rd!","role":"Customer"}'
$customerLogin = Invoke-RestMethod -Method Post -Uri http://localhost:5165/api/auth/login -ContentType "application/json" -Body '{"email":"customer@example.com","password":"Passw0rd!"}'
$customerHeaders = @{ Authorization = "Bearer $($customerLogin.accessToken)" }
Invoke-RestMethod -Method Post -Uri http://localhost:5165/api/reservations -Headers $customerHeaders -ContentType "application/json" -Body "{`"ticketTypeId`":`"$($ticketType.id)`",`"quantity`":2,`"customerEmail`":`"customer@example.com`"}"
```

## Database Migrations

Restore the local EF Core tool and run migrations with:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/Ticketly.Api --startup-project src/Ticketly.Api
```

## Run With Docker Compose

```powershell
docker compose up --build
```

The API is exposed at `http://localhost:8080` and PostgreSQL is exposed at `localhost:5432`.

## SonarQube

Start local SonarQube and its PostgreSQL database:

```powershell
docker compose -f docker-compose.sonarqube.yml up -d
```

Open SonarQube at `http://localhost:9000`. The default local login is usually `admin` / `admin`; SonarQube will ask you to change it on first login. Create a user token in SonarQube and set it in PowerShell:

```powershell
$env:SONAR_TOKEN = "<your-token>"
```

Run tests with OpenCover coverage:

```powershell
.\scripts\run-tests-with-coverage.ps1
```

Run analysis:

```powershell
.\scripts\run-sonarqube-analysis.ps1
```

View results at `http://localhost:9000/dashboard?id=ticketly-plain`.

Stop SonarQube:

```powershell
docker compose -f docker-compose.sonarqube.yml down
```

## Token Estimate

```powershell
.\scripts\estimate-token-burn.ps1
```
