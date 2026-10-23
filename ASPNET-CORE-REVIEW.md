# ASP.NET Core 10 implementation review

Reviewed 20 October 2026. The web project targets net10.0; its MVC/Razor/Identity runtime comes from the ASP.NET Core shared framework. The existing architecture remains MVC with EF Core and SQL Server.

## Implemented

1. **Short-circuit public static assets.** `MapStaticAssets().ShortCircuit()` serves matched wwwroot assets at routing, skipping downstream Identity and authorization work. Framework ETags, content types, build/publish compression and cache behavior remain available. Only public files belong in wwwroot; private applicant uploads must be served by authorized endpoints outside this directory. Static assets intentionally do not execute MVC filters.
2. **Global MVC antiforgery validation.** A single `AutoValidateAntiforgeryTokenAttribute` registration covers unsafe MVC requests, including new controllers. Redundant controller attributes were removed. Existing form Tag Helpers still generate tokens; modal FormData includes them. The error action has a narrowly scoped `IgnoreAntiforgeryToken` because exception-handler re-execution preserves the original HTTP method and must be able to render a failed POST. It performs no mutation.
3. **Request cancellation for read-only data work.** Unit and property list actions accept the MVC-bound cancellation token. Application detail, lease reads and the status-count view component pass RequestAborted to EF. Application-list queries already propagated cancellation. EF operations on a scoped context remain sequential. Mutation transactions retain existing semantics; cancellation of a browser request is not treated as proof of server rollback.
4. **No-store unit-edit dialogs.** Management form responses now match the cache policy already used on other sensitive pages. Public browsing and static-asset caching are unaffected.

## Deliberate choices

- Retain AddControllersWithViews, scoped DbContext/services, async I/O, no-tracking list projections, database pagination and compiled Razor views. No runtime compilation or extra framework packages are needed.
- Retain the framework exception-handler page and HSTS outside Development; do not replace MVC HTML errors with API-oriented JSON globally.
- Do not add blanket response/output caching to personalized pages, generic runtime compression for token-bearing HTTPS responses, EF context pooling with no measurements, or parallel operations on the same DbContext.
- Identity stores, role selection, simple development passwords and disabled migration/history usage remain as requested. No database reset or schema changes were needed.
- Hosting-dependent settings (trusted proxies, AllowedHosts, Data Protection key storage and cookie policy) require the eventual deployment topology. They were not guessed from a local development URL.

## Verification

New tests exercise a test-only controller with no antiforgery attributes: an unsafe request without a token is rejected and a token-bearing request succeeds. The error page remains reachable on POST for error re-execution. A counting claims transformer proves an authenticated static CSS request skips Identity, while /Applications still authenticates and an applicant cannot access manager unit-edit endpoints. A cancellation test verifies read queries observe already-cancelled tokens. Existing core/HTTP workflow tests cover registration, role/ownership, form submissions and concurrency protections.

## Microsoft references

- [ASP.NET Core 10 best practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices?view=aspnetcore-10.0)
- [Static assets and ShortCircuit](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0)
- [MVC antiforgery recommendations](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)
- [RequestAborted and HttpContext](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/use-http-context?view=aspnetcore-10.0)
- [Error handling and re-execution](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0)
