# Current verification — domain keys and rowversions

The current solution-test result is the 2026-10-23 LF install run at the end of this file. Counts in every earlier section are older results from the date written in that section.

Verified October 20, 2026 against the revised development requirements.

- Rider solution build: succeeded with no reported problems.
- Core unit tests: 30 passed.
- Default integration/model/HTTP tests: 14 passed. The opt-in SQL case was skipped in this run and then executed separately.
- Explicit live SQL Server test on `lpc:.\s2025`: passed.
- **All 45 distinct test cases passed across these runs.**

The new model/schema test checks that all ten domain entities have a single integer identity primary key and an eight-byte, database-generated RowVersion concurrency property. It separately checks Identity's normal string IDs and composite keys, with no custom Identity rowversions.

The live SQL test creates an isolated disposable database using EnsureCreated plus seeding. It audits actual SQL Server identity/rowversion column metadata for every domain table, verifies that updating a row changes its native rowversion, rejects a stale EF update, asserts that no __EFMigrationsHistory table exists, checks repeated initialization, and confirms that competing approvals create only one lease. Its disposable test database is deleted afterward.

The application's old `RentalAssessment` development database was explicitly dropped as authorized. Rider successfully recreated and seeded it using the new schema, with standard Identity tables and no EF migration tracking. Business review/status history remains enabled.

The application is running through Rider at https://localhost:7220 (HTTP port 5277). HTTPS requests to home, available units, registration and login returned 200.

## Development lifecycle

- Startup: EnsureCreatedAsync followed by idempotent Bogus seeding.
- No migrations, snapshots, custom history repository or migration-history table.
- Schema changes: stop the app and use the Development-only --reset-database command documented in README.md.
- Domain keys/foreign keys and concurrency handling are updated. Identity stores remain standard, with SecurityStamp and ConcurrencyStamp retained.
- SQLite's rowversion emulation is confined to the test context. Production SQL Server generates rowversions natively.

The schema-only deployment/schema.sql was regenerated directly from the EF model and contains no migration-history statements. Browser-driven JavaScript interaction tests were not added; modal HTTP contracts and underlying workflows remain covered. No GitHub publication or assessment email was performed.

## Welcome page and signed-in application list revision

The current default test run passed 49 cases (30 Core and 19 integration), with the opt-in live SQL case skipped in that run. The previously verified SQL Server schema/concurrency behavior is unchanged by this UI/landing revision.

Five new HTTP test cases confirm that the anonymous welcome page has no application list or counts, protected application pages challenge anonymous access, both registration/login roles land on Applications, and signed-in Home redirects there. Database command capture verifies that status/property predicates and Applicant ownership are in the executed SQL. Tests check exact result IDs for both roles, including exclusion of another applicant's rows and exclusion by status/property. PropertyManagers see all matching rows. Explicit local return URLs are still honored.

Name fields now store FirstName and LastName (required), optional single-letter MiddleInitial, and optional Title selected from shared common honorifics. Registration and application forms validate these fields; display names are composed from their parts. The development database was recreated and reseeded for this schema revision. Validation: 56 default tests passed; the opt-in SQL test was skipped. Live registration page returned HTTP 200 with all four controls.

Query index revision: added Applications(ApplicantId, CreatedAtUtc DESC), Applications(StatusId, CreatedAtUtc DESC), Applications(UnitId, StatusId, CreatedAtUtc DESC), Applications(CreatedAtUtc DESC), and Properties(Name). Applied deployment/apply-query-indexes.sql to RentalAssessment without deleting data or enabling EF history. Verified live sys.indexes/sys.index_columns keys and descending flags. Default tests: 57 passed, one opt-in live SQL test skipped. Existing Identity indexes unchanged.


UI and client-library revision (2026-10-20): redesigned shared navigation, welcome/account pages, application dashboard/wizard, responsive property/history tables and apartment cards. Reviewed the complete Bootstrap component catalog in BOOTSTRAP-DESIGN.md. Bootstrap is 5.3.8; jQuery Validation is 1.22.1; compatible jQuery 3.7.1 and Unobtrusive Validation 4.0.0 are retained. Every runtime third-party asset uses a pinned CDN URL, SHA-384 integrity, anonymous CORS, no-referrer and versioned local fallback. Removed duplicate account-page validation includes and obsolete isolated scaffold CSS. client-assets.json, the integrity-verifying restore script, and .gitattributes preserve runtime fallback bytes.

Validation: 59 tests passed (37 core + 22 integration), with one optional live SQL test skipped. Used a separate bin/UiVerification output to avoid the running app's file locks. The restore script successfully restored and verified all five runtime files. NuGet vulnerability audit including transitive dependencies completed with no known vulnerable packages reported. Browser checks verified the welcome page at narrow and desktop widths, manager sign-in, the application dashboard, status filtering, application details, review modal opening/cancel, and keyboard-operated mobile navbar. At a 390px viewport the page width remained 390px while the history table scrolled inside its own container. Saved dashboard preview: haven-applications.png in the chat outputs folder. Final standard Rider build completed successfully with no reported problems and the HTTPS app restarted successfully. The live registration page returned HTTP 200, served Bootstrap 5.3.8 and Validation 1.22.1, and loaded validation once. Unit execution of the actual generated fallback scripts passed for working CDNs, missing CDNs (all five local files), and incomplete Bootstrap APIs; local fallback tags retain integrity and anonymous CORS.


HTML/template revision: introduced shared PersonName editor and ApplicantInformation, ApplicationStatus and UtcDate display templates following ASP.NET Core 10 conventions. Shared PersonNameInput metadata leaves domain/Identity mappings unchanged. Consolidated modal headers/actions, form-field spacing, validation colors, submit-button layout and read-only definition-list styling. Combined simple Bootstrap card/body wrappers; omitted empty action columns for read-only residence tables. Tests: 60 passed (37 core + 23 integration), one optional live SQL test skipped. The added template regression covers root/nested field prefixes, unique controls, validation associations, invalid-post value retention, Razor encoding, valid saves and read-only summary output.

Final Rider build succeeded with no reported problems and the HTTPS app restarted successfully. Live browser checks confirmed the shared registration editor, read-only applicant display and shared review modal/focus behavior. The anonymous home page now contains 17 div elements versus 20 in the saved pre-refactor rendering (three unnecessary card wrappers removed). No data reset or schema changes were performed.

## Technology review verification — 2026-10-20
Rider solution build passed with no problems. Default suite: 63 passed, one opt-in SQL Server test skipped. NuGet audit including transitive dependencies: no known vulnerabilities reported. `node tools/Test-ModalRequests.cjs` passed stale/closed GET and duplicate-POST checks. Browser smoke checks passed for the application list and property modal loading, focus and dismissal; no warning/error console entries were reported. No schema reset was performed. See TECHNOLOGY-REVIEW.md for scope, tradeoffs and next options.

## ASP.NET Core 10 request pipeline — 2026-10-20
Default suite passed 65 cases (37 core + 28 integration), with one optional SQL Server case skipped; the subsequently added cancellation test passed separately, giving 66 passing distinct cases. New coverage verifies global antiforgery protection, valid-token acceptance, POST error-page compatibility, authenticated static-asset bypass of Identity, preserved role enforcement, and cancellation of read queries. See ASPNET-CORE-REVIEW.md for the implementation and Microsoft documentation. No schema changes/reset.

## Frontend review — 2026-10-20
Rider build passed with no problems. Default suite: 68 passed (37 core + 31 integration), one optional SQL Server test skipped. JavaScript syntax and request-state harness passed, including busy/dismissal state, failure/422 recovery, duplicate submit suppression and obsolete-read cancellation. Browser verified active wizard white-on-green colors, named keyboard-focusable table region, modal validation targets, open/dismiss and no console warnings/errors. App restarted. See FRONTEND-REVIEW.md.

## Verification run — 2026-10-23

Commands were run on 2026-10-23 from the solution directory. Counts below are only what each command printed.

- `dotnet test TroyWebProperty.slnx`: could not run. Exit 127. Exact error: `--: line 1: dotnet: command not found`. No total, passed, failed, or skipped counts were produced. Result: could not run.
- `node tools/Test-ModalRequests.cjs`: passed. Exit 0. Output: `PASS: stale GET ignored; closed GET aborted; duplicate POST suppressed; submit state restored.` The command did not print total, passed, failed, or skipped counts.

README test-count reconciliation: README stated that the latest default test run passed 60 tests, with the optional live SQL Server test skipped. README has no 68-test sentence. The 60 figure matches the HTML/template revision recorded above (60 passed, one optional live SQL test skipped). The 68 figure is the 2026-10-20 frontend-review result recorded above (68 passed, one optional SQL Server test skipped) and the same sentence in FRONTEND-REVIEW.md. This run could not remeasure `dotnet test TroyWebProperty.slnx`, so both earlier counts remain prior results and were not replaced with a new total.

## Verification run — 2026-10-23, SDK install script

Commands were run on 2026-10-23 from the solution directory. Counts below are only what each command printed.

- `bash .cursor/install-dotnet.sh`: could not install the SDK. Exit 2. Exact error: `.cursor/install-dotnet.sh: line 4: set: pipefail` then `: invalid option name`. Bash treated `pipefail` as an invalid `set` option because a carriage return followed that word. The script stopped on that line, before any download. It did not print a `dotnet --version` line. Result: could not install.
- `~/.dotnet/dotnet --version`: not run. The install command failed, so this work item stopped before that call.
- `~/.dotnet/dotnet test TroyWebProperty.slnx`: not run. No total, passed, failed, or skipped counts were produced. Result: not run.

README test-count reconciliation: no new counts were produced. README figures were not changed. The 60 passed and one skipped figure remains the HTML/template result recorded above. The 68 passed and one skipped figure remains the 2026-10-20 frontend-review result recorded above.

## Verification run — 2026-10-23, LF install and test

Commands were run on 2026-10-23 from the solution directory, after `.cursor/install-dotnet.sh` and `.cursor/environment.json` were stored with LF line endings. Counts below are only what each command printed. The test command did not print one combined total; the combined figures are the sum of the two printed assembly summaries.

- `bash .cursor/install-dotnet.sh`: exit 0. The installer reported installed version 10.0.402 and finished successfully. It did not print total, passed, failed, or skipped counts.
- `~/.dotnet/dotnet --version`: exit 0. Output: `10.0.402`.
- `~/.dotnet/dotnet test TroyWebProperty.slnx`: exit 0. Core assembly printed `Failed: 0, Passed: 37, Skipped: 0, Total: 37`. Integration assembly printed `Failed: 0, Passed: 40, Skipped: 1, Total: 41`. Sum of those printed summaries: total 78, passed 77, failed 0, skipped 1. The skipped name printed was `TroyWebProperty.IntegrationTests.SqlServerTests.RealSqlServer_CreatesSeedsRestartsAndSerializesCompetingApprovals`. No test failed. No test was changed.

README and VERIFICATION test-count reconciliation: 77 passed, 0 failed, 1 skipped, total 78 is the current `dotnet test TroyWebProperty.slnx` result, as the sum of the two summaries that command printed. Older results stay the results of the earlier runs above and were not replaced: 45 distinct cases; 49, 56, 57, 59, 60, 63, and 65 passed with the opt-in SQL case skipped; 66 passing distinct cases after the separate cancellation test; and 68 passed with one skipped on 2026-10-20. The two earlier 2026-10-23 attempts recorded above produced no counts (`dotnet: command not found`, exit 127; install script exit 2).

## CI workflow — 2026-10-23

Added `.github/workflows/sqlserver-tests.yml`. The job `sqlserver-tests` runs on `ubuntu-latest` for `pull_request` and for `push` to `refine/round-1`. It uses the service image `mcr.microsoft.com/mssql/server:2022-latest` with `ACCEPT_EULA=Y`, `MSSQL_PID=Developer`, and port 1433. A step generates a random SA password of at least 20 characters with upper case, lower case, digits, and a symbol, masks it with `::add-mask::`, and passes it to the SQL Server process and to `RENTAL_TEST_SQLSERVER` only through environment variables and the step output. The workflow contains no password value. No repository secret is created. The service container is created before steps run, so the password is not placed in the service environment at creation. The container stays open, and a step starts the engine with the password only in that process environment. A sqlcmd login retry waits up to 120 seconds and fails if SQL Server does not accept a login. The job then runs `.cursor/install-dotnet.sh` and `dotnet test TroyWebProperty.slnx`.

`origin pr checks 2` reported no check runs before this workflow was pushed, and again reported no check runs after that push. No CI run has produced totals. There is no CI total, passed, failed, or skipped count, and no CI pass or skip result for `RealSqlServer_CreatesSeedsRestartsAndSerializesCompetingApprovals`.
