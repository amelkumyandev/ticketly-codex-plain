# Experiment Result

Project: ticketly-plain

Experiment type: Without knowledge base

## Summary

Ticketly is a small .NET 10 ASP.NET Core Web API for event ticket reservation. It includes EF Core persistence, PostgreSQL local infrastructure, Docker support, xUnit tests, coverage generation, local SonarQube scaffolding, and token-burn tracking.

No project knowledge base files were added.

## What Was Built

- `GET /health` health endpoint.
- Event endpoints:
  - `POST /api/events`
  - `GET /api/events`
  - `GET /api/events/{id}`
- Ticket type endpoints:
  - `POST /api/events/{eventId}/ticket-types`
  - `GET /api/events/{eventId}/ticket-types`
- Reservation endpoints:
  - `POST /api/reservations`
  - `GET /api/reservations/{id}`
- Business behavior:
  - Event names are required.
  - Ticket types must belong to an existing event.
  - Ticket type price cannot be negative.
  - Ticket type total quantity must be greater than zero.
  - Ticket type available quantity initially equals total quantity.
  - Reservations require positive quantity and customer email.
  - Reservations fail when not enough tickets are available.
  - Successful reservations reduce available quantity.

## Project Structure

- `Ticketly.sln` - solution file.
- `src/Ticketly.Api` - ASP.NET Core Web API.
- `src/Ticketly.Api/Data` - EF Core DbContext and migrations.
- `src/Ticketly.Api/Models` - Event, TicketType, and Reservation entities.
- `src/Ticketly.Api/Requests` - request DTOs.
- `tests/Ticketly.Tests` - xUnit API integration tests.
- `scripts` - PowerShell helper scripts for token estimates, coverage, and SonarQube analysis.
- `coverage` - generated OpenCover coverage output.
- `TestResults` - test result output from coverage collection.

## Database Setup

The API uses EF Core with PostgreSQL through `Npgsql.EntityFrameworkCore.PostgreSQL`. The default Docker Compose file starts:

- API service on `http://localhost:8080`.
- PostgreSQL 18 on `localhost:5432`.
- Database name/user/password: `ticketly` / `ticketly` / `ticketly`.

Migration support is present under `src/Ticketly.Api/Data/Migrations`, with repo-local `dotnet-ef` configured in `dotnet-tools.json`.

## Test Setup

Tests use xUnit and `Microsoft.AspNetCore.Mvc.Testing`. The API switches to EF Core InMemory when the environment is `Testing`, so the integration tests can run without PostgreSQL.

Covered test cases include:

- Creating an event with valid data.
- Creating a ticket type for an existing event.
- Initial available quantity equals total quantity.
- Reservation succeeds when enough tickets are available.
- Reservation reduces available quantity.
- Reservation fails when not enough tickets are available.
- Reservation fails when quantity is zero or negative.

## Coverage Setup

Coverage tooling is configured with `coverlet.collector` and `coverlet.msbuild`.

Coverage commands:

- `dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults`
- `.\scripts\run-tests-with-coverage.ps1`

The SonarQube-oriented OpenCover report is generated at `coverage/coverage.opencover.xml`.

Final coverage result from `.\scripts\run-tests-with-coverage.ps1`:

- Line coverage: 23.22%.
- Branch coverage: 7.08%.
- Method coverage: 51.66%.

## SonarQube Setup

Local SonarQube support is defined in `docker-compose.sonarqube.yml`.

Services:

- `sonarqube` using `sonarqube:lts-community`, exposed at `http://localhost:9000`.
- `sonarqube-db` using PostgreSQL 17.

Analysis script:

- `.\scripts\run-sonarqube-analysis.ps1`

Full SonarQube analysis was not executed in this environment because it requires a running SonarQube instance and a user-generated `SONAR_TOKEN`.

## Token-Burn Tracking

Token tracking files:

- `TOKEN_BURN.md`
- `EXPERIMENT_LOG.md`
- `scripts/estimate-token-burn.ps1`

Tracking method:

- Tasks 0 through 4 record exact platform usage when available from Codex rollout token-count events.
- The local estimator remains available and uses the required reproducible approximation: characters / 4 from `TOKEN_BURN.md`.

Final estimator result:

- Estimated total tokens: 6066.
- Estimated input tokens: 1227.
- Estimated output tokens: 4450.
- Estimation method: characters / 4 from `TOKEN_BURN.md`.

## Verification Results

Final commands run:

- `dotnet build`
- `dotnet test`
- `.\scripts\run-tests-with-coverage.ps1`
- `.\scripts\estimate-token-burn.ps1`

Build result:

- Passed with 0 warnings and 0 errors.

Test result:

- Passed with 8 tests.
- Failed: 0.
- Skipped: 0.

Coverage result:

- Passed and regenerated `coverage/coverage.opencover.xml`.
- Line: 23.22%.
- Branch: 7.08%.
- Method: 51.66%.

SonarQube result:

- Docker Compose and scripts are present.
- Full analysis was not run because local SonarQube credentials are required.

## Manual Fixes Required

- Added repo-local .NET tools for EF migrations and SonarQube scanning.
- Added coverage tooling in both collector and MSBuild forms to support normal test coverage and OpenCover output for SonarQube.
- Kept SonarQube execution as a documented local workflow because the environment does not include a configured token.

## Assumptions

- The final request meant to complete the remaining task from `TICKETLY_PLAIN_CODEX_TASKS.txt`.
- A small Minimal API is sufficient for the required event ticket reservation behavior.
- Tests can use EF Core InMemory to keep automated verification fast and independent from Docker.
- PostgreSQL remains the production/local runtime database through Docker Compose.

## Known Limitations

- Reservation inventory decrement is not protected by an explicit concurrency token or transaction isolation strategy beyond EF Core's normal save flow.
- Customer email is required but not validated for email format.
- Coverage focuses on the required core behavior and does not exhaustively cover all validation and not-found branches.
- SonarQube analysis must be run by a user after starting SonarQube and setting `SONAR_TOKEN`.
- The API does not automatically apply migrations on startup.

## Authentication and Authorization Summary

- JWT implemented: Yes.
- Register endpoint: Yes, `POST /api/auth/register`.
- Login endpoint: Yes, `POST /api/auth/login`.
- Password hashing: Yes, PBKDF2-SHA256 with per-password random salt.
- Role-based authorization: Yes, `Admin` and `Customer` roles.
- Auth tests count: 9 explicit auth/authorization tests, plus existing behavior tests updated to use bearer tokens where required.
- Build result: passed; final `dotnet build` completed with 0 warnings and 0 errors.
- Test result: passed; final `dotnet test --no-build` completed with 17 passed tests.
- Coverage result: passed; line 29.15%, branch 15.27%, method 59.49%.
- SonarQube result if available: not available in this environment. `localhost:9000` is not listening and Docker cannot connect to a local engine pipe.
- Token tracking method: exact platform usage from Codex rollout token-count events.
- Input tokens: 4032444.
- Output tokens: 20145.
- Total tokens: 4052589.
- Local estimator result: estimated total tokens 7619, estimated input tokens 1227, estimated output tokens 5942.
- Manual fixes: generated `AddAuthenticationUsers` EF Core migration, reran tests sequentially after a transient Windows file lock, and documented the SonarQube runtime blocker.
- Known limitations: demo JWT signing key is present only for local development and must be replaced outside local demos; password policy only enforces non-empty passwords; duplicate email checking is application-level plus a unique database index; reservation inventory still lacks explicit concurrency handling.

Auth add-on changes:

- Added `ApplicationUser` persistence with `Email`, `PasswordHash`, `Role`, and `CreatedAt`.
- Added JWT bearer authentication and role policies.
- Protected write/reservation endpoints according to the auth add-on task.
- Added README instructions for JWT configuration, registration, login, bearer token calls, Admin flow, and Customer flow.
- Kept SonarQube compose and scripts in place.
