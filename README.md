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

## API Endpoints

- `POST /api/events`
- `GET /api/events`
- `GET /api/events/{id}`
- `POST /api/events/{eventId}/ticket-types`
- `GET /api/events/{eventId}/ticket-types`
- `POST /api/reservations`
- `GET /api/reservations/{id}`

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
