# Deploying PrintQuote with Docker

PrintQuote runs as three containers:

| Service | Image | Port (in container) | Published on host |
|---|---|---|---|
| `web` | `printquote-web`: nginx serving the React build; forwards `/api/*` to `api` | 8080 | yes, `WEB_PORT` (default 8080) |
| `api` | `printquote-api`: ASP.NET Core (.NET 9) API | 8080 | no |
| `db` | `postgres:16-alpine` | 5432 | no |

The browser only talks to `web`. The page and the API share one origin, so CORS needs no setup. Both app images run as non-root users and have Docker health checks.

```
browser ──HTTPS──> your reverse proxy / LB ──HTTP──> web:8080 ──/api/*──> api:8080 ──> db:5432
```

## Requirements

- Docker Engine 24+ with the Compose plugin (`docker compose version`)
- Both repos checked out side by side, if you build on the server:
  ```
  printquote-backend/    (this repo; holds deploy/)
  printquote-frontend/
  ```
- About 1 GB RAM and 2 GB disk for the images and a small database

## First deployment

```bash
cd printquote-backend/deploy
cp .env.example .env
# edit .env: set POSTGRES_PASSWORD (required) and WEB_PORT if 8080 is taken

docker compose build          # builds printquote-api and printquote-web
docker compose up -d
docker compose ps             # wait until all three show "healthy"
```

Open `http://<server>:<WEB_PORT>`. Checks:

- `GET /healthz`: nginx is up
- `GET /api/health`: the API is up (goes through nginx)

On the first start the API creates the schema. If the database has no presses yet, it also seeds the starting rates from the press's sample sheets: papers, Demy and Double Demy presses, finishing, binding, profit rules, four product templates, two letterheads and one customer. Review and adjust these under **Rates & products** before you issue real quotations.

On a brand-new database the API log shows one `Failed executing DbCommand ... FROM "__EFMigrationsHistory"`. That is expected: EF Core reads the migrations table before it exists, then creates it.

## Building in CI and pulling from a registry

Each repo builds on its own:

```bash
# printquote-backend
docker build -t registry.example.com/printquote-api:1.0.0 .
# printquote-frontend
docker build -t registry.example.com/printquote-web:1.0.0 .
docker push registry.example.com/printquote-api:1.0.0
docker push registry.example.com/printquote-web:1.0.0
```

On the server, set these in `.env`, then pull instead of building:

```
API_IMAGE=registry.example.com/printquote-api
WEB_IMAGE=registry.example.com/printquote-web
IMAGE_TAG=1.0.0
```

```bash
docker compose pull
docker compose up -d
```

In this mode the server needs only the `deploy/` folder; the frontend checkout is not used.

## Updating to a new version

```bash
# set the new IMAGE_TAG in .env (or rebuild), then:
docker compose pull            # or: docker compose build
docker compose up -d           # recreates only the changed containers
```

Pending database migrations are applied when the new API container starts (`MIGRATE_ON_STARTUP=true`). Take a backup first (see below).

## Configuration (`.env`)

| Variable | Default | Meaning |
|---|---|---|
| `POSTGRES_PASSWORD` | none (required) | Database password; the API's connection string is built from it |
| `POSTGRES_DB` / `POSTGRES_USER` | `printquote` | Database name and user |
| `API_IMAGE` / `WEB_IMAGE` / `IMAGE_TAG` | local names, `latest` | Images to run |
| `FRONTEND_DIR` | `../../printquote-frontend` | Frontend checkout, used only by `docker compose build` |
| `WEB_BIND` / `WEB_PORT` | `0.0.0.0` / `8080` | Host address and port for the app |
| `MIGRATE_ON_STARTUP` | `true` | Apply migrations (and seed an empty database) when the API starts |
| `LOG_LEVEL` | `Information` | API log level |

Any other ASP.NET Core setting can be added under `api.environment` in `docker-compose.yml`, with `__` for nesting (for example `Logging__LogLevel__Microsoft.AspNetCore: Warning`).

**Using an external/managed PostgreSQL** (RDS, Azure, etc.): remove the `db` service and the `depends_on: db` block, then set `ConnectionStrings__Default` on `api` to that server, e.g. `Host=db.example.com;Port=5432;Database=printquote;Username=printquote;Password=...;SSL Mode=Require`.

**Running more than one API replica:** set `MIGRATE_ON_STARTUP=false` on the replicas. Apply migrations once per release from one container (`MIGRATE_ON_STARTUP=true`, single instance), or generate a SQL script with `dotnet ef migrations script --idempotent` and run it yourself.

## HTTPS and access control

The stack serves plain HTTP on `WEB_PORT`. Put TLS in front of it: nginx/Caddy/Traefik on the host, or your load balancer. With a proxy on the same host, set `WEB_BIND=127.0.0.1` so the app is reachable only through the proxy.

> **Version 1 has no login.** Anyone who can reach the URL can view and edit rates and quotations. Expose it only on an internal network or VPN, or add authentication at the proxy (basic auth, SSO/OAuth proxy, IP allow-list).

## Backups

All data lives in the `printquote_db-data` Docker volume.

```bash
# backup
docker compose exec -T db pg_dump -U printquote -d printquote -Fc > printquote-$(date +%F).dump

# restore into the running stack (replaces the existing data)
docker compose exec -T db pg_restore -U printquote -d printquote --clean --if-exists < printquote-2026-11-11.dump
```

Schedule the backup (cron or similar) and copy the files off the server.

## Operations

```bash
docker compose logs -f api        # API logs (stdout)
docker compose logs -f web        # nginx access/error logs
docker compose restart api
docker compose down               # stop; data volume is kept
docker compose down -v            # stop AND delete the database volume (destroys all data)
```

Swagger UI is turned off in Production. To browse the API contract, run the API locally in Development (see the repo README) or read `docs/technical-design.md`, section 6.
