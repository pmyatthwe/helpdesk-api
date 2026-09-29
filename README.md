# Helpdesk API

A multi-tenant SaaS helpdesk backend built with ASP.NET Core and PostgreSQL. Multiple companies (tenants) share the same API and database, but each tenant's data is fully isolated, enforced automatically at the data-access layer, not by manually filtering every query.

![CI](https://github.com/pmyatthwe/helpdesk-api/actions/workflows/ci.yml/badge.svg)

## Tech Stack

- **ASP.NET Core 8** (C#)
- **PostgreSQL** with EF Core
- **JWT authentication** with role-based access (Admin, Agent, Customer)
- **Docker** & **Docker Compose**
- **xUnit** for testing
- **GitHub Actions** for CI

## Architecture: How tenant isolation works

Every tenant-scoped table (`Users`, `Tickets`, `Comments`) is automatically filtered by the current tenant, no controller or service ever writes `WHERE TenantId = ...` by hand. 

```
JWT (contains tenantId claim)
        ↓
TenantMiddleware reads the claim → sets ICurrentTenantService
        ↓
HelpdeskDbContext's global query filters use that tenant ID automatically
        ↓
Every EF Core query/save is transparently scoped to the caller's tenant
```

The core of this lives in `HelpdeskDbContext.OnModelCreating`:
```csharp
modelBuilder.Entity<Ticket>()
    .HasQueryFilter(t => t.TenantId == _currentTenant.TenantId);
```

**Why this approach over manual filtering:** centralizing the rule in one place means it's structurally impossible to forget, a new endpoint added six months from now automatically inherits the same isolation guarantee, rather than relying on every developer remembering to add a filter.

**What I'd do differently at scale:** at high tenant counts, a shared-schema design like this one can hit noisy-neighbor performance issues. A schema-per-tenant or database-per-tenant approach trades operational complexity for stronger isolation and per-tenant scaling, a reasonable next step if this were a real production system.

## Data model

| Table | Scoped by |
|---|---|
| `Tenants` | (the tenant itself) |
| `Users` | `TenantId` (direct) |
| `Tickets` | `TenantId` (direct) |
| `Comments` | `Ticket.TenantId` (indirect, via parent ticket) |

## API Endpoints

```
POST   /api/auth/register          Create a new tenant + admin user
POST   /api/auth/login             Log in, returns a JWT

GET    /api/tickets                List tickets (filterable, paginated)
POST   /api/tickets                Create a ticket
GET    /api/tickets/{id}           Get a single ticket with comments
PATCH  /api/tickets/{id}           Update status/assignment (Admin/Agent only)
POST   /api/tickets/{id}/comments  Add a comment

GET    /api/users                  List users in your tenant (Admin only)
POST   /api/users/invite           Add a new user to your tenant (Admin only)
```

## Running it locally

### Option 1: Docker (recommended)

```bash
git clone https://github.com/pmyatthwe/helpdesk-api.git
cd helpdesk-api
cp .env.example .env   # fill in your own values
docker-compose up --build
```

Once running, apply migrations (first time only):
```bash
dotnet ef database update --project HelpdeskApi.Infrastructure --startup-project HelpdeskApi.Api
```

Open **http://localhost:8080/swagger**.

### Option 2: Local .NET + Postgres

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download) and PostgreSQL
2. Copy `.env.example` to `.env` and fill in your connection string and JWT key
3. Run:
   ```bash
   dotnet ef database update --project HelpdeskApi.Infrastructure --startup-project HelpdeskApi.Api
   dotnet run --project HelpdeskApi.Api
   ```
4. Open **https://localhost:5001/swagger**

## Running tests

```bash
dotnet test
```

The test suite specifically verifies tenant isolation, including that a user from one tenant can never see another tenant's tickets or comments, even via a direct query, and that new records are automatically stamped with the correct tenant ID on save.

## Project structure

```
/HelpdeskApi.Api             → Controllers, Program.cs, middleware
/HelpdeskApi.Application     → Services, interfaces (ICurrentTenantService, ITokenService)
/HelpdeskApi.Domain          → Entities (Tenant, User, Ticket, Comment)
/HelpdeskApi.Infrastructure  → EF Core DbContext, migrations
/HelpdeskApi.Tests           → xUnit tests
```