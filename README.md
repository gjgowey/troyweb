# TroyWebProperty — Haven Rentals

.NET 10 ASP.NET Core MVC/Razor rental application system using Identity, EF Core, SQL Server and Bogus. The solution includes property/unit management modals, applicant section forms, residence history, reviews, leases, role/ownership checks and tests.

## Current development database policy

**EF migrations and migration-history tracking are temporarily disabled at your direction.** Startup uses `EnsureCreatedAsync()` followed by idempotent seeding. There are no migration files or snapshots, and the application does not create or use `__EFMigrationsHistory`.

`EnsureCreated` creates missing databases/tables; it does not upgrade an existing schema. After changing the model, recreate the development database explicitly:

```powershell
dotnet run --project TroyWebProperty.Web --launch-profile https -- --reset-database
```

This command drops the configured development database, creates the current schema and seeds it. Stop any running copy first. The reset flag is accepted only in Development. Ordinary startup preserves domain data and user changes, except that Development startup sets every existing account password to the shared test password described below.

When the model is stable, enable migrations as a separate change with a new initial baseline. No migration history or data conversion from earlier prototypes is being retained.

## Primary keys and concurrency

All ten application/domain tables have a single **`Id int IDENTITY(1,1)` primary key** and a non-null, SQL Server-generated **`RowVersion rowversion`** column:

- PropertyManagers, Applicants, Properties and Units
- UnitTypes and ApplicationStatuses
- Applications, Residences, Leases and StatusChanges

Lookup codes remain unique business identifiers, with integer foreign keys for unit types and application statuses. They are not primary keys.

**Identity retains the standard ASP.NET Core schema.** Users and roles have the default string IDs; memberships, logins and tokens retain their standard composite keys. The built-in SecurityStamp and ConcurrencyStamp remain. No custom Identity store or Identity rowversion columns are used. EF infrastructure tables are also exempt from the domain-table convention.

RowVersion is an automatically generated eight-byte SQL Server value, not a timestamp/date or GUID. EF uses it for optimistic concurrency. Edit forms submit Base64 rowversions; stale application/property/unit changes are rejected. Residence edits also update the parent application so its rowversion advances. Identity's normal concurrency checks remain independent.

## Setup

Install the .NET 10 SDK and run SQL Server/Express. The supplied instance is configured in `TroyWebProperty.Web/appsettings.json`:

```
Server=lpc:.\s2025;Database=RentalAssessment;Integrated Security=True;TrustServerCertificate=True;Encrypt=True
```

The Windows account running the app needs connection, database creation and schema permissions. `lpc:` requires a local SQL Server instance. The trusted-certificate setting is for the development instance.

```powershell
dotnet restore
dotnet dev-certs https --trust
dotnet run --project TroyWebProperty.Web --launch-profile https
```

Open https://localhost:7220. Signed-out visitors see a generic welcome page with login, registration and apartment-browsing links. After login or registration, users land on Applications; signed-in visits to Home also redirect there. The application list requires authentication, filters status/property in SQL, shows Applicants only their own rows, and shows PropertyManagers all matching rows. In Rider, use the existing **TroyWebProperty: https** run configuration. HTTP uses port 5277.

To override the connection in Development:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=YOUR-SERVER;Database=RentalAssessment;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" --project TroyWebProperty.Web
```

Other environments can use `ConnectionStrings__DefaultConnection`. Keep real database credentials and production passwords out of source control; the shared application password below is intentionally documented for function testing. A schema-only script generated from the current EF model is in `deployment/schema.sql`; it does not use migration history or seed data.

## Function-testing logins

All 27 seeded accounts use the same password: **`password`** (lowercase).

| Role | Example login | All seeded logins | Password |
| --- | --- | --- | --- |
| PropertyManager | `manager1@example.test` | `manager1@example.test` through `manager3@example.test` | `password` |
| Applicant | `applicant1@example.test` | `applicant1@example.test` through `applicant24@example.test` | `password` |

Log in at https://localhost:7220/Account/Login. Account numbers have no leading zeros. These are local sample accounts; access to an email inbox is unnecessary because email confirmation is disabled.

- PropertyManagers can see all applications and manage properties and units.
- Applicants see only their own applications. Status and property filters execute in the database.
- Seeded accounts receive the shared password when created or when missing a password.
- **Every Development startup sets all existing accounts, including accounts created through registration, to `password`.** Changed passwords therefore revert on the next Development startup. Password hashes are stored using ASP.NET Identity; passwords are not stored as plaintext in the database.
- Registration requires a nonempty password (1–100 characters), with no uppercase, lowercase, digit or symbol requirements. Confirmation must match. A newly registered account initially uses its submitted password; after a Development restart it uses `password`.
- Identity locks an account after five failed login attempts for five minutes. Use another seeded account or wait for the lockout to expire.
- Database reset deletes accounts and other data, then recreates the 27 seeded accounts. These shared credentials and relaxed rules are for the current function-testing setup.

Verified live sign-in for `manager1@example.test` and `applicant1@example.test` with `password`. The current `~/.dotnet/dotnet test TroyWebProperty.slnx` on 2026-10-23, with `~/.dotnet/dotnet --version` printing 10.0.402, printed core Failed 0, Passed 37, Skipped 0, Total 37 and integration Failed 0, Passed 40, Skipped 1, Total 41. The sum of those printed summaries is 77 passed, 0 failed, 1 skipped, and 78 total. The skipped case is the opt-in live SQL Server test. Older results stay labeled as older in VERIFICATION.md: the HTML/template run is 60 passed and one skipped, and the 2026-10-20 frontend-review section is 68 passed and one skipped. Earlier on 2026-10-23, `dotnet test TroyWebProperty.slnx` could not run (`dotnet: command not found`, exit 127), and `bash .cursor/install-dotnet.sh` exited 2 before any download; those attempts produced no counts. `node tools/Test-ModalRequests.cjs` passed.

## Person names

Registration and applicant information use required First and Last name, optional single-letter Middle Initial, and an optional Title dropdown with common personal, professional, religious and military honorifics. Names are stored separately in Applicants, PropertyManagers and Applications; full display names are composed from those parts. Standard ASP.NET Identity tables retain their normal schema.

## Walkthrough

1. Log in as `applicant1@example.test` using `password`, or register as **Applicant**. Browse available units and start an application.
2. Save Applicant Information, add prior residences through the modal, save Residence History, review the Summary, then Submit.
3. Log in as `manager1@example.test` using `password`, or register another account as **PropertyManager**, using a second browser/profile if convenient. Open Applications, filter by property/status, and review a submitted application.
4. Return requires a comment; the applicant can correct and resubmit. Deny requires a comment and is terminal. Approval creates a twelve-month lease starting on the selected date.
5. As PropertyManager, add/edit/remove properties and units through modals. Invalid submissions return the same form partial with errors; success closes the modal and refreshes the affected region.

Sign-up intentionally permits either of these two roles, per the assessment. Whitelisted roles, new Identity user and corresponding applicant/manager profile are saved transactionally before sign-in. Login/logout, antiforgery, local redirects and Identity lockout are implemented. Email confirmation is disabled for this assessment.

All seeded accounts use the function-testing password `password`. In Development, startup also sets all existing accounts to this password. Registration accepts simple passwords without character-complexity rules. PropertyManagers are company-wide staff and can review all applications/manage all properties; Applicants see only their own applications. A property's manager identifies its assigned creator rather than restricting access to other company staff.

## Workflow rules

Statuses: **Draft, Submitted, Returned, Approved, Denied, Withdrawn**. Approved, Denied and Withdrawn are terminal. Only Draft/Returned applications are editable. Withdraw is available from Draft, Returned or Submitted.

One wizard view model and form posts to one action. Continue validates/saves only the current section; Back does not save. Summary renders both saved sections read-only, with Submit available only after both sections are saved. The same section partials render editable/read-only based on server decisions. Every write checks role, applicant ownership, state and concurrency.

Residence add/edit/remove uses modals and invalidates the history-section save flag. At least one complete prior residence is required. Move-out cannot precede move-in or be in the future.

Unit types have an active flag. Inactive types are omitted for new selections, rejected by the server for other units, and retained/displayed on existing units that already use them. Unit numbers are unique within a property. Removing properties containing units or units with applications/leases is rejected to preserve business history.

Availability uses UTC “today”. Lease start is inclusive and end is exclusive: `end = start.AddMonths(12)`. Submit and approval recheck active leases. Approval additionally rejects overlapping future leases. Transactions acquire a write lock on the unit to serialize competing approvals. Other applications for that unit are left unchanged.

The application list applies ownership, status and property predicates in SQL. `ApplicationCountsViewComponent` renders role-scoped totals. Review audit history records who, when, outcome and comment and is shown to managers; applicants see their latest return comment. This business history is retained and is unrelated to EF migration history.

## Seeding

Startup inserts missing records by stable unique SeedKey/lookup code/normalized Identity name, preserving edits and avoiding duplicates. Bogus generates names, phones, addresses, rents and property names with local random seeds. A SQL transaction and application seed lock serialize seed runs without EF migration machinery.

Initial data: two roles, three managers, 24 applicants, four properties, 12 units, three unit types (one inactive), six statuses, 24 applications (four per status), residence/status histories, and four leases. Seeded lease terms cover the initial seed date. Seed accounts use `example.test` addresses and the shared password `password`.

## Tests

```powershell
dotnet test TroyWebProperty.slnx
```

Core tests isolate business rules. Default HTTP integration tests use SQLite and the actual MVC, Identity, repository and service code. A test-only context emulates eight-byte rowversion changes because SQLite does not support SQL Server rowversion; production never generates rowversion values in application code.

The model-convention test verifies all ten domain keys/rowversions and verifies that Identity remains standard. The SQL Server create-script test confirms the schema has no migration-history table.

Enable the live SQL Server test:

```powershell
$env:RENTAL_TEST_SQLSERVER = 'Server=lpc:.\s2025;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test tests/TroyWebProperty.IntegrationTests --filter FullyQualifiedName~RealSqlServer
```

The live test creates a disposable `RentalAssessment_Test_<guid>` database, creates/seeds it without migrations, audits actual SQL identity/rowversion metadata, verifies native rowversion changes and stale-update rejection, checks restart idempotency, and races approvals for a single lease. It drops only its disposable database afterward. Use a test server/account with create/drop permission.

## Continuous integration

`.github/workflows/sqlserver-tests.yml` runs on pull requests and on pushes to `refine/round-1`. The `sqlserver-tests` job uses `ubuntu-latest` and the service image `mcr.microsoft.com/mssql/server:2022-latest` with `ACCEPT_EULA=Y`, `MSSQL_PID=Developer`, and port 1433. Each run generates a random SA password in a step (at least 20 characters, with upper case, lower case, digits, and a symbol), masks it immediately, and passes it to SQL Server and to `RENTAL_TEST_SQLSERVER` only through environment variables and the step output. The workflow file contains no password value. No repository secret is created. The job retries a SQL login for up to 120 seconds and fails if the server does not accept a login. It then runs `.cursor/install-dotnet.sh` and `dotnet test TroyWebProperty.slnx`. Setting `RENTAL_TEST_SQLSERVER` runs `RealSqlServer_CreatesSeedsRestartsAndSerializesCompetingApprovals`. Leaving that variable unset or blank skips that test. A local run without the variable stays opt-in.

## Structure and scope

- `TroyWebProperty.Core`: domain models, role constants and business rules.
- `TroyWebProperty.Infrastructure`: EF mapping, standard Identity, database initializer/seeder, repository and transactional service.
- `TroyWebProperty.Web`: MVC controllers, view models, Razor views/partials/view component, modal JavaScript and styles.
- `tests`: business unit tests and HTTP/model/live SQL integration tests.

Optional assessment bonuses are not implemented. Migration tracking is intentionally deferred by the revised development requirements. No GitHub repository has been published and no assessment email sent. See `VERIFICATION.md` for current results.

## Query indexes

Indexes are configured in `ApplicationDbContext`, included in `deployment/schema.sql`, and applied to the current RentalAssessment database. For an existing database with the current schema, run `deployment/apply-query-indexes.sql` with sqlcmd `-b -I` to apply this index revision without dropping data or using EF migrations. The script is repeatable and transactional. Fresh databases receive the indexes through EnsureCreated.

| Table / key columns | Query supported |
| --- | --- |
| Applications: ApplicantId, CreatedAtUtc DESC | Applicant ownership filter and newest-first list |
| Applications: StatusId, CreatedAtUtc DESC | Status filter and newest-first list; status counts |
| Applications: UnitId, StatusId, CreatedAtUtc DESC | Property-to-unit join, optionally status; unit dependency checks |
| Applications: CreatedAtUtc DESC | Manager list without filters |
| Properties: Name | Property list and filter dropdown ordering |
| Units: PropertyId, Number (existing unique) | Property filter/join, duplicate unit numbers, units within a property |
| Leases: UnitId, StartDate, EndDate (existing) | Active lease and overlapping lease checks |
| Leases: RentalApplicationId (existing unique) | Application lease lookup |
| Applicants / PropertyManagers: UserId (existing unique) | Logged-in user's domain profile |
| Lookup Code / SeedKey (existing unique) | Status/type lookup and idempotent seeding |

Redundant single-column Applications indexes on ApplicantId, StatusId and UnitId are replaced by composite indexes with the same leading columns. EF's other foreign-key indexes, primary keys and standard Identity indexes are retained. No extra indexes are added on RowVersion, person names, or the low-selectivity UnitTypes.IsActive flag: current queries do not justify them. Residence/history ordering currently happens after loading an application's rows, so date indexes there would not speed up a database sort.

Composite indexes support their leading columns; arbitrary combinations of optional filters may still need residual filtering and sorting. The lease index can seek by unit and a start-date range, with end-date checked as a residual predicate. These choices follow the current query shapes; production-sized execution plans should guide further tuning. SQL Server may correctly choose scans for the small seeded dataset.

## Appearance and Bootstrap components

The interface uses the Bootstrap 5.3.8 with a local Haven theme: responsive role-aware navigation, status cards and badges, a filter panel, responsive tables, styled account forms, application breadcrumbs/section navigation and improved modal loading/focus states. The generic welcome page remains available to signed-out users. See `BOOTSTRAP-DESIGN.md` for the complete component review, selection rationale and official references.

Bootstrap CSS and the JavaScript bundle load from the version-pinned jsDelivr CDN with SHA-384 integrity, `crossorigin="anonymous"`, and `referrerpolicy="no-referrer"`. Matching local files provide fallbacks through ASP.NET Core Tag Helpers. Bootstrap loads before the application script; the local theme follows Bootstrap CSS. The bundled JavaScript includes Popper.

## Client-library maintenance

All third-party runtime CSS/JavaScript uses version-pinned jsDelivr URLs, SHA-384 integrity, anonymous CORS, no-referrer, and matching versioned local fallbacks. Script order is jQuery, jQuery Validation, Unobtrusive Validation, Bootstrap bundle, then site.js. Validation scripts load once globally, including support for dynamically fetched modal forms. Local site.css and site.js use ASP.NET content-version query strings.

| Library | Version | Notes |
| --- | --- | --- |
| Bootstrap | 5.3.8 | Bundle includes Popper; CSS also has a local fallback. |
| jQuery | 3.7.1 | Retained for compatibility: the unobtrusive adapter calls $.parseJSON and $.isFunction, removed in jQuery 4. |
| jQuery Validation | 1.22.1 | Updated from 1.21.0; supports current validation workflows. |
| Unobtrusive Validation | 4.0.0 | Current released adapter; retained. |

`client-assets.json` records exact CDN URLs, local runtime paths and integrity hashes. Restore those files with `pwsh -File tools/Restore-ClientAssets.ps1`; the script verifies each hash before replacing a file. When updating a version, update the manifest and corresponding Razor integrity/URL attributes together. Integration tests check matching bytes, fallback markup, ordering and duplicate loads.

Compatibility references: https://jquery.com/upgrade-guide/4.0/ and https://github.com/aspnet/jquery-validation-unobtrusive/releases and https://github.com/jquery-validation/jquery-validation/releases

Vendor files are marked `-text` in `.gitattributes` to preserve exact integrity-checked bytes across Git checkouts. The NuGet vulnerability audit (including transitive dependencies) reported no known vulnerable packages on 2026-10-20; backend package versions were unchanged by the UI/library review.

Removed the unused scaffold `_Layout.cshtml.css` so the old template theme does not linger alongside the Haven theme.

## Razor HTML and shared templates

Following the ASP.NET Core 10 DisplayTemplates/EditorTemplates conventions, `Views/Shared/EditorTemplates/PersonName.cshtml` renders the same validated name controls at registration and under the application's `Info` prefix. Shared `PersonNameInput` metadata keeps their annotations consistent without changing domain entities or Identity tables. `DisplayTemplates/ApplicantInformation.cshtml`, `ApplicationStatus.cshtml` and `UtcDate.cshtml` render read-only details, status badges and semantic UTC dates. Summary and read-only screens do not render editable applicant fields.

Modal headers and action buttons use shared partials. Form field spacing, validation colors, account submit buttons, action rows and detail-list layout live in `wwwroot/css/site.css`; Bootstrap control classes, cards, responsive grids, alerts and modal attributes remain where they express the component's behavior. Card and card-body share a single element where a separate header/body wrapper is unnecessary. Inputs preserve MVC validation, unique IDs and model-binding prefixes; required first/last-name controls also use native `required`, with autocomplete and error-description associations. Modal triggers specify `type="button"` to avoid unintended form submission.

This reduces duplicated source and unnecessary DOM wrappers. It does not claim a measured network-speed improvement or strip meaningful user-content whitespace. No schema change or database reset is needed.

Reference: https://learn.microsoft.com/en-us/aspnet/core/mvc/views/display-templates?view=aspnetcore-10.0

### Icons and typography
Font Awesome Free 7.3.1 supplies solid icons for navigation, statuses, filters and actions. Only the free core and solid styles are loaded; no kit, account or subscription is required. Decorative icons are hidden from assistive technology and retain visible text labels. CDN styles use SRI, anonymous CORS, no-referrer and independent local CSS fallbacks. The icon font also falls back locally if the CDN font fails. License notices are retained in `wwwroot/lib/fontawesome/LICENSE.txt`.

Inter variable is used for interface text; Source Serif 4 variable is used for headings and display numerals. Both are free under SIL Open Font License 1.1, with licenses in `wwwroot/fonts`. Latin normal WOFF2 fonts load from a version-pinned jsDelivr URL first, then an identical local copy on failure. `font-display: swap` preserves text rendering, with system font fallbacks for other characters. `font-assets.json` and `fontawesome-assets.json` record versions and integrity. Existing Bootstrap and JavaScript CDN fallbacks remain in place.

### Technology review
See `TECHNOLOGY-REVIEW.md` for implemented query, caching and modal improvements and a prioritized roadmap with primary documentation. Application lists now use database pagination (25 rows/page), preserve filters and order ties by ID. Run the JavaScript request-regression checks with `node tools/Test-ModalRequests.cjs`.

### ASP.NET Core 10 configuration
See `ASPNET-CORE-REVIEW.md`. MVC antiforgery validation is global; new state-changing forms must submit the generated token. Public assets use MapStaticAssets().ShortCircuit(); never store private files under wwwroot. Read queries observe request cancellation.

### Frontend accessibility and interaction
See `FRONTEND-REVIEW.md`. Validated styled inputs/selects/textareas use shared accessibility Tag Helpers; include matching validation spans. Nonvalidated filter selects use `data-no-field-validation`. Wide tables are named keyboard-scrollable regions. Modal saves announce busy state and prevent dismissal until completion. Run `node tools/Test-ModalRequests.cjs` for interaction-state checks.
