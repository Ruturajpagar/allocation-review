# Design notes

These notes explain the main decisions, the trade-offs behind them, and the assumptions made where the brief left room. They are written so an associate developer can pick up a follow-up change.

## Architecture

```
Angular SPA ──/api──▶ ASP.NET Core API ──EF Core──▶ PostgreSQL
 (routes, guards,       Controllers  → thin HTTP layer: binding, validation, status codes
  interceptor)          Services     → queries (RunReviewService)
                        Domain       → business rules (GapCalculator), no framework code
```

The API has one project with folders by responsibility (Domain, Data, Runs, Auth, Infrastructure), not separate class libraries. For a read-only feature this size, extra projects would add ceremony without adding isolation. The domain rules have no dependency on ASP.NET or EF, so they could move into their own library later without changes.

Responsibilities:

- **`GapCalculator`** (Domain): the single source of the calculation rules, namely rounding, sum, floor at zero and status. It is pure and fully unit-tested. Both the API and the seed checks rely on it.
- **`RunReviewService`** (Runs): the read queries. It projects straight to DTOs with `AsNoTracking`, and lets the database do the sums.
- **`RunsController`** (Runs): HTTP only. It validates input (400), maps "not found" to 404, and adds the run ID to the logging scope. Authorization is declared with an attribute, `[Authorize(Policy = CanReviewRuns)]`.
- **Infrastructure:** cross-cutting concerns. It adds a correlation ID to every request and keeps error output consistent and safe.

## Data model

```
allocation_runs  (id PK varchar(32), run_date date)
   index (run_date DESC, id DESC)              ← matches the history ORDER BY exactly
allocation_gaps  (id PK, run_id FK, gap_code, store, brand, product_type, original_shortfall numeric(12,2))
   unique (run_id, gap_code)                   ← "a gap ID is unique within its run"
   check  (original_shortfall >= 0)
batch_allocations (id PK, gap_id FK, batch_id, quantity int)
   unique (gap_id, batch_id), check (quantity >= 0)
```

- **Normalised into three tables.** The brief's flat rows repeat the gap's shortfall on every batch row. Storing the shortfall once per gap makes "count the original shortfall once" true by construction, instead of relying on every query to de-duplicate.
- **A gap with no allocation has zero batch rows.** The brief's "None / 0" row means exactly that. The seed loader drops `batchId: null` rows rather than storing fake batches.
- **The shortfall is stored as `numeric`.** "Rounded up to a whole unit" implies it can be fractional. The raw value is stored and rounded when reviewed, so the original stays auditable.
- **Store, brand and product type are plain columns on the gap.** They are only displayed and grouped by, so lookup tables would add joins for no benefit. If they gain attributes of their own, such as a store region, they would become reference tables. That change is mechanical.
- **Business IDs are kept as they are.** Run IDs (`R-102`) are the natural primary key because users refer to runs by them. Gap codes (`G-01`) are unique only within a run, so the gap table uses a surrogate key plus a composite unique constraint.
- **Seeding is driven by data.** `Data/Seed/allocation-runs.json` holds the brief's example rows in the same flat shape as the brief's table. The loader groups them by run and gap and rejects rows that disagree about a gap's details.

## Key trade-offs

1. **Offset pagination instead of keyset pagination.**
   - Paging uses `ORDER BY run_date DESC, id DESC OFFSET/LIMIT`, backed by a matching index.
   - The tie-breaker on `id` makes the order total and stable: pages never overlap or skip rows, even when several runs share a date. A test walks every page to prove this.
   - Offset paging lets the UI show "Page 3 of 7" and jump straight to any page.
   - Keyset (cursor) paging would scale better to very deep pages and handle concurrent inserts better, but it can't jump to page N.
   - At the volumes a merchandising team saves runs, offset is the simpler and better fit.
   - The page size is capped at 100 so a request can't ask for everything.

2. **Aggregation in SQL, status rules in C#.**
   - The database sums batch quantities per gap and per run, so detail rows aren't pulled into memory.
   - The status and rounding rules run in `GapCalculator` on one row per gap. That keeps the rules in one tested place instead of duplicating them in SQL `CASE` expressions.
   - The exceptions filter is also applied after the rules run, so "exception" can never mean something different in SQL and in C#.
   - The cost: a run's gaps are loaded together. That is fine for one dispatch's gap list. If runs ever had tens of thousands of gaps, the gap list would get its own pagination and the status would move into a SQL view.

3. **A cookie session (Identity) instead of JWT bearer tokens.**
   - The session lives in an HttpOnly, SameSite=Strict cookie. Scripts can't read it, so an XSS bug can't steal it.
   - SameSite=Strict, a single origin and JSON-only endpoints protect against CSRF.
   - Sign-out takes effect on the server.
   - The trade-off is that third-party or mobile clients would need a token scheme. The brief only has this SPA, which is served from the same origin.
   - Identity provides password hashing, lockout (5 failures locks for 5 minutes) and roles without custom crypto.

4. **SQLite in memory for API tests (PostgreSQL in the app).**
   - The integration tests host the real app: the real pipeline, auth, validation, EF model and seed file.
   - Only the provider is swapped, so `dotnet test` needs no Docker or network ("without access to client systems").
   - The model avoids provider-specific features to keep the two in step.
   - What this cannot catch: PostgreSQL-specific SQL translation or collation differences. `scripts/smoke-test.sh` covers that gap by running the acceptance checks over HTTP against the real PostgreSQL build.
   - The next step would be Testcontainers in CI.

5. **Serving the SPA from the API in production.** It's one deployable on one origin, with no CORS and no cross-site cookie settings. Splitting the two later (for example, the SPA on a CDN) would need `SameSite=None`, CORS and CSRF tokens. That's a deliberate change, not a configuration flag.

## Security and logging

- **Authorization is enforced on the server.** The policy `CanReviewRuns` requires the Admin role on both review endpoints. A *fallback* policy makes every other endpoint require a signed-in user unless it is explicitly `[AllowAnonymous]`, so a new endpoint is safe by default. The UI guards only shape navigation; the API is the real gate.
- **The API never redirects.** An API that redirects to a login page gives SPAs HTML instead of status codes, so the cookie events map login and access-denied redirects to 401 and 403.
- **Errors are controlled.**
  - All errors are ProblemDetails.
  - Unhandled exceptions are logged with their stack trace and returned as a generic 500, plus the correlation ID.
  - Validation errors list the bad parameters.
  - Sign-in gives the same answer for an unknown email and a wrong password, which prevents account enumeration.
- **Correlation IDs.**
  - The middleware reuses a caller's `X-Correlation-ID` only if it matches `^[A-Za-z0-9-]{1,64}$`, which prevents log injection. Otherwise it creates a new one.
  - It puts the ID in a logging scope, so every log line from that request carries it, and returns it in the response header and in error bodies.
  - The SPA sends a fresh ID with every request and shows the ID on error panels as a "reference for support".
  - Review requests also add `RunId` to the scope.
- **What is never logged:** passwords, cookies, tokens, headers, request bodies, query strings or email addresses. Sign-in logs use the internal user ID. The request log records method, route path, status and duration only. One production-build check searched the JSON log for the demo passwords and the word "password" and found zero matches.

## Failure case and how to diagnose it (walkthrough)

**Scenario:** a merchandiser reports "Run R-102 shows *Something went wrong*".

1. The error panel shows **Reference for support: `3f2c…`**. That is the correlation ID.
2. Search the JSON logs for `"CorrelationId":"3f2c…"`. Every line from that request appears:
   - the request line (`HTTP GET /api/runs/R-102 responded 500 in … ms`);
   - the `GlobalExceptionHandler` error, with the exception type and stack trace;
   - the `RunId` scope.
3. Typical causes and what the log shows:
   - **Database unreachable or credentials rotated:** an `NpgsqlException` or a connection timeout. `/health` is still up because it doesn't check the database. Adding a DB health check is a natural next step.
   - **Bad data,** such as a negative shortfall inserted outside the app: `GapCalculator` throws `ArgumentOutOfRangeException`. The database check constraints should stop this at write time.
4. A 401 or 403 is not a server fault. The request log shows the status, and the UI already handles it: back to sign-in, or the access-denied page.

## Guiding an associate through a follow-up change

**Example: add a "Store" filter to the run review.**

1. **API contract.** Add `string? store` to `GetReview` in `RunsController`. Validate its length and return 400 for bad input, following the existing `filter` validation.
2. **Query.** In `RunReviewService.GetReviewAsync`, filter `AllocationGaps` by store *before* projecting, so the database does the work. Decide whether the run totals should describe the whole run or the filtered set. Today totals are deliberately whole-run, so keep that and document it.
3. **Tests first.** Add cases to `RunReviewTests`: one store, an unknown store giving an empty list, and an over-long value giving 400. Run `dotnet test`.
4. **UI.** In `RunsApi.getReview`, add the parameter. In `RunDetail`, add a `store` input (it binds from `?store=` automatically) and a `<select>`. Add a step to `review-flow.spec.ts` that checks the request parameter.
5. **Docs.** Update the API table in the README.

Conventions that keep this safe: business rules only in `Domain/`, queries only in services, controllers stay thin, every new endpoint gets an explicit policy, and every user-visible state (loading, empty, error) gets a test.

## Assumptions

1. **Both endpoints are Admin-only.** The brief says to "enforce Admin access to both review endpoints". Viewers can sign in, see who they are and sign out, but can't see runs. The UI explains this instead of showing an empty page.
2. **"Total allocated units"** for a run is the sum of all its batch quantities (R-102 = 2+1+2+0 = 5). It is not capped at each gap's shortfall. Over-allocation shows in the totals, but the gap's *remaining* never goes below 0.
3. **Rounding happens before subtraction.** A shortfall of 4.2 becomes 5; allocating 4 leaves 1 and the gap is *partially filled*, not filled. Allocated quantities are whole units (integers).
4. **A zero shortfall with no batches is Filled,** because zero units remain. That follows the brief's rule order.
5. **History sort:** run date descending, then run ID descending as the tie-breaker. Run dates are calendar dates (`date`), with no time zone.
6. **Exceptions filter:** totals and status counts always describe the whole run, so the user keeps context. Only the gap rows are filtered. Gap rows are ordered by gap ID.
7. **Invalid input:**
   - `page` must be between 1 and 100,000, and `pageSize` between 1 and 100. Anything else, including non-numbers, returns 400.
   - A page past the end returns 200 with an empty list, because the request is valid and there is simply no data.
   - An unknown `filter` value returns 400.
8. **Session length:** a 30-minute sliding idle timeout. When it expires, the next API call returns 401, and the SPA returns to sign-in with "Your session expired" and comes back to the same page afterwards.
9. **Demo users** are created only when their passwords are configured. Development config provides them; production config deliberately does not.
10. **Tests in the stack the brief names:** the brief allows React or Angular and asks for "at least one user-facing React flow" test. This solution uses Angular, so that requirement is met with an equivalent Angular flow test.
