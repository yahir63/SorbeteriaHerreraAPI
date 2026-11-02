# AGENTS.md

## Stack

.NET 10 (`net10.0`), ASP.NET Core with top-level-statements `Program.cs`, EF Core 10 +
SQL Server, NUnit 4. No CI (`.github/workflows/` is empty), no `.editorconfig`, no
`Directory.Build.props`, no `global.json`, no linter/formatter. Don't assume any
automated checks exist.

## Layout

Solution: `HerreraSystemAPI.slnx` (note: the repo folder is `SorbeteriaHerreraAPI`).

| Folder | Role |
|---|---|
| `HerreraSystemAPI/` | Entrypoint `Program.cs` (DI + JWT + Swagger all inline), `Controllers/`, `Middleware/`, `Services/` (`CurrentUserService`, image upload). csproj is `HerreraSystem.API.csproj`. |
| `HerreraSystem.Application/` | `Common/` (`ApiResponse`, `ServiceResult`, `PagedResponse`, `PaginationParams`, `IUnitOfWork`, `Constants`), `DTOs/<Module>Dtos/`, `Interfaces/Repositories/`, `Interfaces/Services/`, `Services/`. Business logic + DI-free orchestration. |
| `HerreraSystem.Infrastructure/` | `Data/HerreraSystemContext.cs`, `Repositories/`, `Persistence/UnitOfWork.cs`, `Migrations/`. |
| `HerreraSystemDomain/` | EF-scaffolded entities. Folder has **no dot**, csproj is `HerreraSystem.Domain.csproj`, namespace is `HerreraSystem.Domain.Entities`. |
| `HerreraSystem.Tests/` | NUnit fixtures. |

Adding a feature means: entity → `IXRepository` + `XRepository` → `IXService` + `XService`
→ `XDto` → `XController` → register all three interfaces in `Program.cs`. Missing the
`Program.cs` registration is the most common cause of a runtime DI failure here.

## Commands

```bash
dotnet build HerreraSystemAPI.slnx
dotnet run --project HerreraSystemAPI          # http://localhost:5177, https://localhost:7035
dotnet test HerreraSystem.Tests
dotnet test HerreraSystem.Tests --filter "FullyQualifiedName~OrderServiceTests"
dotnet test HerreraSystem.Tests --filter "Name~CreateOrder_"
dotnet ef migrations add <Name> --project HerreraSystem.Infrastructure --startup-project HerreraSystemAPI
```

- **The `.slnx` sets `<Build Solution="Debug|*" Project="false" />` for the Tests
  project**, so `dotnet build` on the solution silently skips tests. Build/test
  `HerreraSystem.Tests` directly.
- Swagger (`/swagger`) only when `ASPNETCORE_ENVIRONMENT=Development`.
- `app.UseHttpsRedirection()` is always on. With the `https` launch profile both
  ports bind and HTTP 5177 307-redirects to HTTPS 7035 — curl the HTTPS URL.
- EF commands need the `dotnet-ef` global tool.
- There is nothing to lint or format-check; `dotnet build` warnings are the only signal.

## Environment

- `dotnet` is **not on PATH in the WSL shell**. The Windows SDK (10.0.400) is at
  `/mnt/c/Program Files/dotnet/dotnet.exe`.
- Tests use `Trusted_Connection=True`, so they only work when run from Windows.
- CRLF line endings. `.gitattributes` has `* text=auto`.

## Conventions

- **Every** endpoint returns `ApiResponse<T>` (`{ success, message, data }`). Never
  return bare objects. ModelState failures are auto-wrapped by the
  `InvalidModelStateResponseFactory` in `Program.cs`.
- Services return `ServiceResult<T>` (`Success` / `ErrorMessage`) and controllers map
  that to `ApiResponse`. Don't throw for validation or business-rule failures.
- `ExceptionMiddleware` returns **HTTP 500 with the raw `ex.Message`** for every
  unhandled exception. Exception type does not map to a 4xx — return errors explicitly.
- User-facing strings are **Spanish**, and so are the docs. Keep new messages in Spanish.
- `[Authorize]` is applied **per write action, not per controller** — only
  `Products`, `Orders`, `Sales`, `Restocks` have it, and only on POST/PUT/PATCH/DELETE.
  Reads are public by design. See Known Risks.
- List endpoints bind `PaginationParams` (`Page` default 1, `PageSize` default 10,
  hard-capped at 50) and return `PagedResponse<T>` via `.ToPagedResponseAsync()`.
- **Timestamps for inventory, movements and orders must come from
  `INicaraguaDateTimeService`** (`America/Managua`, UTC-06:00), injected into the
  repository/service. Never `DateTime.Now`. `AuthService` and `UserService`
  deliberately use UTC for token expiry and audit fields — don't "fix" them.
  User-entered dates (e.g. batch expiry) are never converted.
- Read queries use `.AsNoTracking()`.
- `UnitOfWork` implements a nested-transaction depth counter, so nested
  `BeginTransactionAsync` / `CommitAsync` in helper methods is safe by design.
  Always `SaveChangesAsync` then `CommitAsync`, and `RollbackAsync` on every error path.
- Money columns are `decimal(10, 2)`; precision is declared inside
  `HerreraSystemContext.OnModelCreating`.
- `Infrastructure/Extensions/PaginationExtensions.cs` lives in Infrastructure but
  declares `namespace HerreraSystem.Application.Common`. That's intentional.
- Images are stored on local disk at `wwwroot/uploads/{products,flavors}` (extension
  allow-list: `.jpg .jpeg .png .webp`). Stale `Azure.*.dll` files remain in `bin/`
  from before this moved off Azure Blob — ignore them, the code has no Azure dependency.
- CORS policy is named `PermitirFrontend` (`AllowAnyOrigin`).
- JWT claims: `sub` and `ClaimTypes.NameIdentifier` (user id), `ClaimTypes.Name`,
  `FullName`, `JwtRegisteredClaimNames.Email`, and one `ClaimTypes.Role` per role.
  `CurrentUserService` reads these.

## Testing

**These are integration tests against a live local SQL Server, not unit tests.**
Each fixture's `SetUp` hardcodes:

```
Server=.;Database=HerreraSystem;Trusted_Connection=True;TrustServerCertificate=True;
```

- Despite the reference, `Microsoft.EntityFrameworkCore.InMemory` is **unused** — every
  fixture calls `.UseSqlServer(...)`.
- The local `HerreraSystem` DB must already exist with the schema and seeded catalog data.
  Fixtures hardcode `LocationId == 1` (Bodega) and `LocationId == 3` (Reservado) and a
  batch with a known bodega stock.
- Fixtures **insert real rows and best-effort delete them in `TearDown`**. Never point
  them at anything but a throwaway local dev database.
- Services and repositories are constructed by hand in each `SetUp` with a
  `FakeCurrentUserService` (id 1, role Admin) and a `FakeConfiguration`. Adding a
  constructor dependency to a service means updating all four fixtures.
- Requires Windows (see Environment).

## Migrations & data

- Only one migration exists: `20260831034835_InitialCreate`. There is no `HasData`,
  no `InsertData`, and no `Database.Migrate()` call in `Program.cs` — the schema is
  **not** applied automatically. Use `dotnet ef database update` explicitly.
- No roles or users are seeded, and `AuthController` exposes only `login`, so the first
  admin user has to be inserted directly into the database.
- `HerreraSystemContext.cs` and everything in `HerreraSystemDomain/Entities/` are
  EF-scaffolded (`partial`, `partial void OnModelCreatingPartial`). A re-scaffold
  overwrites them. Keep custom behavior in services, or add new partial files.

## Docs

Nine Spanish `DOCUMENTACION_*.md` files at the repo root are the **frontend contract**.
Update the matching one whenever you change an endpoint contract or DTO shape.

| File | Module |
|---|---|
| `DOCUMENTACION_AUTORIZACION_JWT.md` | Auth, JWT claims, which endpoints are protected |
| `DOCUMENTACION_CATALOGOS_PRODUCTOS_FRONTEND.md` | Lines, Presentations, LinePresentations, Flavors |
| `DOCUMENTACION_MODULO_PRECIOS_FRONTEND.md` | Prices |
| `DOCUMENTACION_MODULO_PRODUCTOS.md` | Products |
| `DOCUMENTACION_MODULO_INVENTARIO.md` | Inventory (locations, batches, stock) |
| `DOCUMENTACION_MODULO_MOVIMIENTOS_INVENTARIO.md` | Inventory movements |
| `DOCUMENTACION_MODULO_REABASTECIMIENTOS_FRONTEND.md` | Restocks |
| `DOCUMENTACION_MODULO_VENTAS_FRONTEND.md` | Sales |
| `DOCUMENTACION_MODULO_LOGS.md` | Audit logs |

## Known risks

Don't regress these, and don't copy any of them into new files or commit messages.

- `HerreraSystemAPI/appsettings.json` is **committed** with a live remote SQL Server
  `sa` password and the JWT signing key. `appsettings.Development.json` does not
  override the connection string, so `dotnet run` in Development hits that same remote
  database. Treat both values as secrets: never echo them, never paste them into
  issues/PRs/docs, never commit new credentials to this file.
- **16 of 20 controllers have no `[Authorize]` at all**, including `UserController`,
  `RolesController`, `UploadsController`, `InventoryController`, `PricesController`
  and `CustomersController` — user/role management and file upload are reachable
  unauthenticated. When you add a controller, follow the existing write-only
  convention or deliberately lock it down; flag the gap rather than silently shipping
  another open endpoint.
- Tests mutate a live database (see Testing).
- `HerreraSystem.Domain/` (with the dot, at the repo root) is a **dead leftover**: 30
  entity files, no `.csproj`, not in the solution, and it declares the same
  `HerreraSystem.Domain.Entities` namespace as the live `HerreraSystemDomain/`.
  Only `User.IdNumber` differs. Never edit it and never add it to the solution (that
  would produce duplicate type definitions). It is a safe delete candidate once
  confirmed.

## Git

Branch is `main`, only 2 commits, no PR or release automation to follow.