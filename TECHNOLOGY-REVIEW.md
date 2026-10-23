# Technology review — 20 October 2026

This review follows the current rental-assessment requirements and the explicit development overrides. It covers the MVC/Identity request pipeline, EF queries and transactions, SQL indexing/concurrency, Razor/Bootstrap accessibility, CDN assets/fonts, modal JavaScript and test strategy. It is a code and documentation review, not a load test, penetration test or formal WCAG audit.

## Implemented

| Change | Reason and limits |
|---|---|
| Database pagination, 25 applications per page | Ownership/status/property predicates, count and page selection execute in SQL. Order is CreatedAtUtc DESC, Id DESC, making ties deterministic. Invalid/out-of-range page numbers clamp safely; links preserve filters and applying filters starts at page 1. Offset paging suits this small app; deep pages and concurrent inserts/deletes are reasons to consider keyset paging later. |
| Split read-only application-detail query | Residences and status changes are sibling collections; loading them separately avoids multiplicative joined rows. It adds round trips and does not promise an atomic snapshot. Rowversions and command-side serializable transactions still validate writes. Command/repository queries are deliberately unchanged. |
| No-store on application, account and property-management controllers | Covers detail and modal responses as well as lists; prevents compliant HTTP caches storing these pages. This is HTTP cache policy, not a guarantee about browser history behavior. Static assets retain their normal caching. |
| Cancel obsolete modal GETs | Closing/reopening the modal aborts pending reads and generation checks reject stale responses, including responses whose body completes later. Mutating POSTs are not aborted because browser cancellation cannot guarantee server rollback. |
| Suppress repeated modal submissions | A form-in-flight flag guards repeated submit events in addition to the disabled submit button; cleared on completion/error. Server rowversion/transaction protections still apply. |
| Icon spacing | Explicit trailing-icon flag avoids treating a leading icon followed by plain text as a trailing icon. Labels remain visible and decorative icons remain hidden from screen readers. |

No database schema changes or resets were needed. Existing development password rules, sample logins, role selection, standard Identity tables, domain integer IDs/rowversions and migration-free startup are retained.

## Existing choices worth keeping

- .NET 10 / EF Core 10.0.13: current servicing level checked against Microsoft's support policy. Avoid a framework rewrite just to change the UI.
- Standard Identity stores, role authorization on controllers, ownership predicates on data access, anti-forgery validation and local-only return URLs.
- Pure business-rule tests plus HTTP tests and a separate real-SQL-Server test. SQLite tests cannot prove SQL Server locking or rowversion behavior.
- No-tracking projections for lists, targeted filter indexes, and serializable unit locking for availability-sensitive commands.
- MapStaticAssets already supplies framework static-asset optimizations. Do not add redundant caching middleware for personalized pages.
- Bootstrap 5.3 components, shared Razor display/editor templates, semantic labels/validation, local CSS, and version-pinned CDN assets with local fallbacks. Free icon/font licenses remain included.

## Options for the next iterations

| Priority | Option | Benefit | Tradeoff / decision needed |
|---|---|---|---|
| Next | CI build, core/HTTP tests and isolated SQL Server tests on every change | Finds provider-specific locking/concurrency failures and regressions early | Choose Git hosting/runner and disposable SQL database provisioning; do not point CI at RentalAssessment. |
| Next | Browser automation plus automated accessibility checks | Exercises modal races, keyboard focus, validation, paging and responsive layouts in a real browser | Adds browser test infrastructure; include keyboard/screen-reader and contrast checks manually too. |
| Next | Extend the asset restore script to fonts and Font Awesome | One repeatable restore/upgrade process and verified CDN/local byte identity for every third-party asset | Additional maintenance; preserve SRI, pinned versions and license notices. Existing script currently handles its original client-assets manifest. |
| Before deployment | Separate production account policy from testing defaults | Configurable account recovery, email confirmation, MFA/passkeys and invitation-based manager assignment | Needs an email provider and decisions about who may become a manager. Current self-registration of either role is intentional for the assessment. |
| Before deployment | Login/registration rate limits with correct proxy handling | Complements per-account lockout and bounds expensive password hashing | Tune against actual traffic; shared IPs can penalize legitimate users; per-process limits need a distributed/edge strategy across servers. |
| When schema stabilizes | Enable EF migrations and deployment-controlled schema updates | Reproducible upgrades with reviewed SQL and backups | Explicitly deferred by your instruction. Do not silently enable migration history now. |
| At hosting selection | Persist/protect Data Protection keys; health checks, structured logs and telemetry | Stable cookies across deployments and diagnosable failures | Configuration depends on IIS/container/cloud and number of instances. Keep credentials and applicant PII out of logs. |
| With larger datasets | Keyset pagination and query-plan/Query Store measurement | Better deep-page performance and evidence for index changes | Different navigation semantics; current OFFSET paging and COUNT cost grow with data. Add indexes only after measuring read benefit versus write cost. |
| With workflow needs | Notifications, review queues, document uploads and retention | Useful applicant/manager features | Define business rules, storage, file scanning, privacy and retention first; these are product scope changes. |

## Verification

Default suite after changes: 63 passed (37 core + 26 integration); one opt-in SQL Server test skipped. The new integration cases exercise SQL paging, equal-date ordering, ownership/filter preservation, page bounds, sensitive-page cache headers and separate collection loads. `node tools/Test-ModalRequests.cjs` covers stale/closed modal reads and duplicate submit suppression. Current NuGet audit including transitive dependencies reports no known vulnerable packages from configured sources; this is not a security certification.

## Primary references

- [ASP.NET Core performance guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices?view=aspnetcore-10.0)
- [EF efficient querying](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying)
- [EF pagination and unique ordering](https://learn.microsoft.com/en-us/ef/core/querying/pagination)
- [Single versus split queries and consistency tradeoffs](https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries)
- [EF concurrency and rowversion](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
- [Identity configuration](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0)
- [Role authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-10.0)
- [Response cache controls](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/response?view=aspnetcore-10.0)
- [Rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
- [Data Protection configuration](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0)
- [Static assets](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0)
- [EF database test strategy](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy)
- [Bootstrap accessibility](https://getbootstrap.com/docs/5.3/getting-started/accessibility/)
- [Fetch cancellation](https://developer.mozilla.org/en-US/docs/Web/API/AbortController)
- [SQL Query Store](https://learn.microsoft.com/en-us/sql/relational-databases/performance/monitoring-performance-by-using-the-query-store)
- [.NET support and servicing](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
