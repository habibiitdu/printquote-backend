# PrintQuote: backend

ASP.NET Core (.NET 9) API that prices offset-printing jobs and stores quotations in PostgreSQL. It serves the React app in `../printquote-frontend`.

The full design (printing terms, the costing rules with a worked example, data model and API contract) is in [docs/technical-design.md](docs/technical-design.md).

## Prerequisites

- .NET SDK 9
- PostgreSQL 15 or newer: a local service, or `docker compose up -d` (port 5433, see `docker-compose.yml`)
- EF Core tools: `dotnet tool install -g dotnet-ef`

## Run it

```bash
# 1. Point the API at your database (stored outside the repo)
dotnet user-secrets --project src/PrintQuote.Api set "ConnectionStrings:Default" \
  "Host=localhost;Port=5432;Database=printquote;Username=postgres;Password=<your password>"

# 2. Start the API on http://localhost:5080
dotnet run --project src/PrintQuote.Api
```

In Development the API applies pending migrations on startup. If the database has no presses yet, it also seeds the rates from the press's sample sheets: papers, Demy and Double Demy presses, finishing, binding, profit rules, four product templates, two letterheads and one customer.

- Swagger UI: http://localhost:5080/swagger
- Health check: http://localhost:5080/api/health
- Example requests: `src/PrintQuote.Api/PrintQuote.Api.http`

## Tests

```bash
dotnet test
```

The engine tests reproduce the press's sample cost sheets. For example, English (2) at 25,000 copies prices at 16.38 per piece.

## Database migrations

```bash
# After changing an entity or AppDbContext:
dotnet ef migrations add <Name> --project src/PrintQuote.Infrastructure --output-dir Migrations

# Apply without starting the API:
dotnet ef database update --project src/PrintQuote.Infrastructure \
  --connection "Host=localhost;Port=5432;Database=printquote;Username=postgres;Password=<your password>"
```

## Project layout

| Project | Contents |
|---|---|
| `src/PrintQuote.Domain` | Entities and enums |
| `src/PrintQuote.Application` | `Costing/` (the pure costing engine), `Services/`, `Dtos/` |
| `src/PrintQuote.Infrastructure` | EF Core `AppDbContext`, migrations, seed data |
| `src/PrintQuote.Api` | `Program.cs`, controllers, error handling |
| `tests/PrintQuote.Application.Tests` | xUnit tests for the engine |

## Configuration

| Key | Default | Meaning |
|---|---|---|
| `ConnectionStrings:Default` | none (required) | PostgreSQL connection string |
| `Database:MigrateOnStartup` | `true` in Development, otherwise `false` | Apply migrations and seed an empty database on startup |
| `Cors:AllowedOrigins` | `http://localhost:5173` in Development | Origins allowed to call the API |

## Deploying to a server

Docker images and a production Compose stack (PostgreSQL + API + nginx web) are set up. See [deploy/README.md](deploy/README.md).

- `Dockerfile`: the API image (`printquote-api`, port 8080, non-root, health check on `/api/health`)
- `deploy/docker-compose.yml` + `deploy/.env.example`: the full stack
- `docker-compose.yml` in the repo root is only the local development database.
