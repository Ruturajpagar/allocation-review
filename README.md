# Allocation Run History and Exception Review

A small full-stack app for a retail merchandising team. It lists saved stock allocation runs and reviews one run: what it allocated, and which replenishment gaps are still open.

- **Front end:** Angular 22 (standalone components, signals, reactive forms)
- **Back end:** ASP.NET Core on .NET 10 (controllers, EF Core 10, ASP.NET Core Identity)
- **Database:** PostgreSQL 17 (run locally with Docker)

Scope follows the brief: sign-in, run history and run review. There is no allocation engine, registration, password recovery, shipment processing or cloud deployment.

Design decisions, trade-offs and assumptions are in [docs/DESIGN.md](docs/DESIGN.md).

---

## What it does

| Area | Behaviour |
|---|---|
| **Run history** | `GET /api/runs?page=&pageSize=` returns saved runs newest first, with run ID, run date and total allocated units. Paging happens on the server, with a default page size of 20 and a maximum of 100. Sort order is stable: run date descending, then run ID descending. The UI keeps the page number in the URL. |
| **Run review** | `GET /api/runs/{runId}?filter=all\|exceptions` returns the run total, gap counts by status, and gap rows: store, brand, product type, original shortfall, allocated, remaining and status. The `exceptions` filter shows only partially filled and unfilled gaps. Totals and counts always describe the whole run. |
| **Sign-in and access** | ASP.NET Core Identity with an HttpOnly, SameSite=Strict session cookie. There are two seeded demo users, **Admin** and **Viewer**. Both review endpoints are Admin-only on the server: no session gets **401**, a Viewer gets **403**. The UI hides the reviews from Viewers and shows an "access denied" page. |
| **Error handling** | Every error is an RFC 9457 ProblemDetails response with a `correlationId`. An unknown run returns **404**. Invalid paging or filter input returns **400**. Unexpected failures return a generic **500**; details go only to the log. The UI handles loading, empty history, empty exception lists, expired sessions (back to sign-in with a note), unknown runs and server or network errors with a retry button. |
| **Logging** | Structured logs. Outside Development they are JSON on stdout. Each request gets a correlation ID: taken from `X-Correlation-ID` if it is safe, otherwise generated. The ID is echoed in the response and attached to every log line, and review requests also carry the run ID. Logs never contain passwords, cookies, tokens, request bodies or query strings. |

### Calculation rules

These are implemented once, in `GapCalculator`:

- **Grouping:** rows are grouped by run and gap ID. The original shortfall is counted once per gap and rounded **up** to a whole unit.
- **Allocated:** the sum of the gap's batch quantities.
- **Remaining:** `max(0, shortfall − allocated)`.
- **Status:**
  - **Filled** when 0 units remain.
  - **Unfilled** when units remain and nothing was allocated.
  - **Partially filled** otherwise.

**Expected result for R-102:** 3 gaps and 5 allocated units.
- G-01: shortfall 5, allocated 3, remaining 2, partially filled.
- G-02: shortfall 2, allocated 2, remaining 0, filled.
- G-03: shortfall 4, allocated 0, remaining 4, unfilled.
- The exceptions filter shows G-01 and G-03.
- G-01 in R-101 is a separate gap.

---

## Prerequisites

| Tool | Version used | Notes |
|---|---|---|
| .NET SDK | 10.0.404 (pinned in `global.json`, rolls forward to newer 10.0.x) | https://dotnet.microsoft.com/download/dotnet/10.0 |
| Node.js | 24.x (20.19+ or 22.12+ also supported by Angular 22) | npm 11 |
| Docker | Docker Desktop / Engine with Compose | Only needed for the local PostgreSQL. To use an existing PostgreSQL 14+ instead, see [Configuration](#configuration). |
| EF Core CLI (optional) | `dotnet tool install --global dotnet-ef --version 10.0.15` | Only needed to **create new** migrations. Applying them works without it. |

---

## Quick start (from a clean checkout)

Run these from the repository root.

```bash
# 1. Start PostgreSQL (localhost:5433, database allocation_review)
docker compose up -d

# 2. Create the schema and seed the two synthetic runs and the two demo users
dotnet run --project api/src/AllocationReview.Api -- seed

# 3. Start the API on http://localhost:5153
dotnet run --project api/src/AllocationReview.Api --launch-profile http

# 4. In a second terminal: start the Angular dev server on http://localhost:4200
cd web
npm ci
npm start
```

Open **http://localhost:4200** and sign in.

| Demo user | Email | Password | Can review runs |
|---|---|---|---|
| Admin | `admin@demo.local` | `Admin#Demo2026` | Yes |
| Viewer | `viewer@demo.local` | `Viewer#Demo2026` | No. Sees "No access to run reviews"; the API answers 403. |

The demo passwords are local development values from `appsettings.Development.json`. In any other environment you supply them yourself, or leave them unset to skip creating the users. See [Configuration](#configuration).

Step 2 is optional in Development. There, `Database:MigrateOnStartup` and `Database:SeedOnStartup` are on, so step 3 also migrates and seeds. Seeding is idempotent, so running it twice is safe.

> The first migration logs one "Failed executing DbCommand … `__EFMigrationsHistory`" line. This is normal EF Core behaviour while it checks whether the history table exists, and it continues straight away.

### Demo walkthrough

1. Sign in as **Admin**. The run history shows **R-102** (14 Sep 2026, 5 units) above **R-101** (13 Sep 2026, 1 unit).
2. Open **R-102**:
   - totals: 5 units, 3 gaps, 1 filled, 1 partially filled, 1 unfilled;
   - the three gap rows match the brief.
3. Click **Exceptions only (2)**. Only G-01 and G-03 remain. The URL becomes `/runs/R-102?filter=exceptions`.
4. Open `/runs/R-999` to see the "Run not found" state, with a support reference.
5. Sign out and sign in as **Viewer**. You land on "No access to run reviews".

---

## Running the tests

```bash
# Back end: 53 xUnit tests (unit + API integration). Needs no database, Docker or network.
dotnet test

# Front end: 7 Vitest tests of user-facing flows, in jsdom
cd web
npm ci
npm test -- --watch=false
```

### What the tests cover

**Back end** (`api/tests/AllocationReview.Api.Tests`):

- **Multi-batch calculation** (`Domain/GapCalculatorTests`):
  - summing batch rows;
  - counting the shortfall once and rounding it up, including rounding before subtracting;
  - zero floor when over-allocated;
  - every status;
  - which statuses count as exceptions.
- **Seed grouping** (`Domain/AllocationSeedFileTests`): brief rows become runs, gaps and batches, and inconsistent rows are rejected.
- **History pagination** (`Api/RunHistoryPaginationTests`):
  - newest first, with correct totals;
  - walking every page returns each run exactly once in a stable order, including several runs on the same date;
  - repeat requests return the same page;
  - pages past the end are empty;
  - defaults;
  - invalid input returns **400**: `page=0`, `-1`, `1.5`, overflow, `pageSize=0`, `101` and `abc`.
- **API authorization** (`Api/AuthorizationTests`):
  - 401 without a session and 403 for a Viewer, on both endpoints;
  - Admin is allowed;
  - cookie flags;
  - wrong password and unknown account give the same answer;
  - malformed sign-in returns 400;
  - sign-out ends the session.
- **Run review** (`Api/RunReviewTests`):
  - R-102 exactly as in the brief;
  - the exceptions filter;
  - R-101 G-01 is separate;
  - 404 for an unknown run;
  - 400 for an unknown filter;
  - the correlation ID is echoed, and an unsafe one is replaced.

**Front end** (`web/src/app/features/review-flow.spec.ts`), driving the real routes, guards and interceptor with a fake HTTP backend:

- **Admin flow, end to end:**
  1. redirected to sign-in, fills in the form;
  2. sees the history newest first and opens R-102;
  3. checks the totals and rows;
  4. switches to **Exceptions only**: the API is called with `filter=exceptions` and only G-01 and G-03 are shown.
- A Viewer is sent to "No access".
- An expired session (401 in the middle of a session) goes back to sign-in with a note.
- Not found, server error with retry, empty history, and the wrong-password message.

The API integration tests host the real application with `WebApplicationFactory`, using SQLite in memory. It's built from the same EF Core model and seeded from the same seed file. The trade-off is explained in [docs/DESIGN.md](docs/DESIGN.md#testing). `scripts/smoke-test.sh` runs the brief's acceptance checks over HTTP against a running instance backed by PostgreSQL. It needs bash and curl.

---

## Production build

```bash
# Windows PowerShell
./scripts/build-prod.ps1
# macOS / Linux / Git Bash
./scripts/build-prod.sh
```

The script:
1. Runs an optimised Angular build (`ng build --configuration production`).
2. Publishes the API in Release mode.
3. Copies the Angular files into the API's `wwwroot`.

The output is one self-contained app in `artifacts/app`. The API serves the UI and the API from **one origin**, so the session cookie can stay `SameSite=Strict` and no CORS is needed. Deep links such as `/runs/R-102` fall back to `index.html`; unknown `/api/*` paths return a JSON 404.

To run it locally (PowerShell example):

```powershell
$env:ConnectionStrings__Default = 'Host=localhost;Port=5433;Database=allocation_review;Username=allocation;Password=allocation_dev'
$env:DemoUsers__AdminPassword  = 'Admin#Demo2026'    # optional: only if you want the demo users
$env:DemoUsers__ViewerPassword = 'Viewer#Demo2026'
$env:Session__RequireHttps     = 'false'             # ONLY for trying it on plain http://localhost
$env:ASPNETCORE_URLS           = 'http://localhost:5180'
dotnet artifacts/app/AllocationReview.Api.dll seed   # migrate + seed, then exit
dotnet artifacts/app/AllocationReview.Api.dll        # start
```

This build was checked locally: `scripts/smoke-test.sh http://localhost:5180` passed **16/16**, logs came out as JSON, and the UI was clicked through in Chrome.

---

## Configuration

Settings are layered by standard ASP.NET Core rules, later sources winning:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. user secrets (Development only)
4. environment variables, written with `__` for nesting (for example `ConnectionStrings__Default`)
5. the command line

| Setting | Development (`appsettings.Development.json`) | Test (integration tests) | Production |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` (launch profile) | `Testing` (set by the test host) | `Production` (the default when unset) |
| `ConnectionStrings:Default` | local Docker PostgreSQL on port 5433 | not used: SQLite in memory, wired by the test factory | **required**: environment variable or secret store. The app refuses to start without it. |
| `Database:MigrateOnStartup` | `true` | `true` (builds the schema) | `false`: run `AllocationReview.Api seed` or apply migrations in the deployment step |
| `Database:SeedOnStartup` | `true` | `true` | `false` |
| `DemoUsers:AdminPassword` / `ViewerPassword` | local demo values | test values in the factory | unset by default, so no demo users are created |
| `Session:IdleTimeoutMinutes` | 30 (sliding) | 30 | 30 |
| `Session:RequireHttps` | off | off | **on**: Secure cookie, HSTS, HTTP→HTTPS redirect |
| Logging | readable single-line console with scopes | warnings only | JSON console with scopes (correlation ID, run ID, trace ID) |

**API base URL in the front end.** The Angular app always calls relative `/api/...` URLs:
- In development, `web/proxy.conf.json` forwards `/api` to `http://localhost:5153`. Edit it if the API runs elsewhere.
- In production, the UI is served by the API itself, so there is nothing to configure.

**Secrets.** No real secret is committed. The only credentials in the repository are the local Docker database password and the demo passwords, both for development only. For anything shared, use environment variables or `dotnet user-secrets` (the project has a `UserSecretsId`).

---

## API reference

All error responses are `application/problem+json` and include a `correlationId`.

| Method and path | Auth | Success | Errors |
|---|---|---|---|
| `POST /api/auth/login` body `{ email, password }` | anonymous | 200 `{ email, roles, canReviewRuns }` and sets the session cookie | 400 invalid body, 401 wrong credentials or locked out (5 failures locks for 5 minutes) |
| `POST /api/auth/logout` | anonymous | 204 | none |
| `GET /api/auth/me` | signed in | 200 `{ email, roles, canReviewRuns }` | 401 |
| `GET /api/runs?page=1&pageSize=20` | **Admin** | 200 `{ items: [{ runId, runDate, totalAllocatedUnits }], page, pageSize, totalCount, totalPages }` | 400, 401, 403 |
| `GET /api/runs/{runId}?filter=all\|exceptions` | **Admin** | 200 `{ runId, runDate, totalAllocatedUnits, gapCount, statusCounts: { filled, partiallyFilled, unfilled }, filter, gaps: [...] }` | 400, 401, 403, 404 |
| `GET /health` | anonymous | 200 `Healthy` | none |
| `GET /openapi/v1.json` | anonymous, Development only | OpenAPI document | none |

---

## Project layout

```
api/
  src/AllocationReview.Api/
    Domain/          AllocationRun, AllocationGap, BatchAllocation; GapCalculator (the business rules)
    Data/            AppDbContext (schema), DatabaseInitializer (migrate + seed), Migrations/, Seed/allocation-runs.json
    Runs/            RunsController (HTTP), RunReviewService (queries), RunContracts (DTOs, paging rules)
    Auth/            AuthController (login/logout/me), AuthSetup (Identity, cookie, policies), Roles
    Infrastructure/  CorrelationIdMiddleware (IDs + request log), GlobalExceptionHandler (500s), ProblemDetails setup
    Program.cs       composition root and HTTP pipeline
  tests/AllocationReview.Api.Tests/
    Domain/ Api/ Infrastructure/ (ApiFactory: in-memory host + SQLite)
web/
  src/app/
    core/            models, AuthService, guards, api.interceptor (correlation ID + expired session), RunsApi, load-state, http-error
    features/        login, run-history, run-detail, access-denied, not-found, review-flow.spec.ts
    shared/          error-panel
    testing/         app-harness (real routes + fake backend), test-data
scripts/             build-prod.ps1 / build-prod.sh, smoke-test.sh
docs/DESIGN.md       design notes, trade-offs, assumptions, walkthrough notes
docker-compose.yml   local PostgreSQL
```
