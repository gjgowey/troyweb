# Assessment implementation map

All paths below are relative to the solution directory. Optional bonuses are explicitly excluded; a view component is included because it is mandatory independently of the optional grid.

| Mandatory item | Main implementation | Verification |
|---|---|---|
| MVC controllers, view models, Razor | `TroyWebProperty.Web/Controllers`, `Models`, `Views` | HTTP tests render pages and forms |
| Partial views | `Views/Applications/_ApplicantSection.cshtml`, `_HistorySection.cshtml`, `_Summary.cshtml`, `_History.cshtml`, modal partials | Workflow and modal HTTP tests |
| View component | `ViewComponents/ApplicationCountsViewComponent.cs`, `Views/Shared/Components/ApplicationCounts/Default.cshtml` | Application-list rendering tests |
| Populate/validate/save modals | Property, unit, residence and review GET/POST actions; `wwwroot/js/site.js` | HTTP tests assert same partial with 422, success JSON and persisted state |
| .NET 10 | All project files target `net10.0` | Solution build |
| Identity and two role choices | `TroyWebProperty.Core/AppRoles.cs`, `Controllers/AccountController.cs`, account views, navigation partial | Registration, login/logout, role whitelist, ownership and UI tests |
| EnsureCreated plus idempotent seeding run in every environment; only --reset-database and the Development password reset are Development-only | `Program.cs`, `Infrastructure/Data/DatabaseInitializer.cs`, direct model creation | SQL Server create-script/model tests and real SQL Server test |
| Idempotent Bogus seed | `Data/DatabaseSeeder.cs`, `SeedCatalog.cs`, EF seed callbacks | Seed rerun and every-status coverage test |
| Business unit tests | `tests/TroyWebProperty.Core.Tests/ApplicationRulesTests.cs` | 30 business-rule cases |
| SQL Server / Express | `UseSqlServer` in `DatabaseConfiguration.cs`, direct model schema creation | SQL schema script generation and opt-in live SQL verification |
| Sign-up/login/logout | `AccountController`, `Views/Account` | Real Identity cookie HTTP tests |
| Property/unit add/edit/remove | `PropertiesController`, `UnitsController`, respective Razor views | Modal CRUD, validation and persistence tests |
| Active/inactive unit type | `UnitType.IsActive`, `ApplicationRules.UnitType`, `UnitsController.Types/Edit` | Core tests plus inactive-new rejection and inactive-existing preservation HTTP tests |
| Available unit browsing | `UnitsController.Index`, `Views/Units/Index.cshtml` | Approved unit disappears from browse results |
| Applicant information | `ApplicantInformation`, wizard information partial, `SaveInformationAsync` | Field validation and valid-only persistence tests |
| Prior residences | `ResidenceForm`, residence modal, `SaveResidenceAsync` | Invalid dates, add/edit/remove tests |
| One-page wizard | `ApplicationEditModel`, `ApplicationsController.Edit`, shared section partials | Continue/Back/Summary/Submit HTTP flow |
| Back without saving | Edit command dispatch | Posted values are not persisted test |
| Draft/Returned editable only | `ApplicationRules.CanEdit`, application/residence POST checks | State-matrix unit tests and terminal read-only HTTP checks |
| Ownership | Query predicates and service mutation guard | Other applicant receives 404 and no list entry |
| Active lease at submit/approval | `ApplicationService`, `RentalRepository.LockUnitAsync/OccupiedAsync` | Business tests, approval-vs-existing-lease HTTP flow and opt-in competing approvals test |
| Review outcomes/comments | Review modal, `ApplicationRules.Review`, `ReviewAsync` | Return/resubmit/approve and commented-denial flows |
| Terminal statuses | `ApplicationRules` | State matrix and post-withdrawal checks |
| Twelve-month lease | Approval service and `Lease` mapping | Lease end equals start plus twelve months; overlap/second-lease rejection |
| Who/when/comment audit | `StatusChange`, service `ChangeAsync`, manager history partial | Review history persists and renders only for managers |
| Database filters | LINQ query predicates in `ApplicationsController.Index` before `ToListAsync` | Status/property/ownership list checks |
| README and handoff | `README.md`, `.gitignore`, `VERIFICATION.md` | Source ZIP inspection |

## Optional scope

Not implemented: JSON/OpenAPI grid, sorting bonus, claim/release review queue, manager-only notes, saving invalid sections, multiple applicants and per-section multi-applicant concurrency. The implementation includes a basic whole-application stale-version guard but does not claim that bonus.

## Delivery limits

The code targets `lpc:.\s2025`, database `RentalAssessment`. Rider successfully started the application against that instance and created the schema and seeded it. The live SQL Server test also passed, including competing approvals. Across the default suite and the explicitly enabled SQL test, all 45 distinct cases passed. The normal HTTP suite uses SQLite strictly as an isolated test provider; SQL Server behavior is separately verified by the live test.

The source is ready to upload to a GitHub repository. No GitHub repository has been published and no assessment email has been sent. The README supplies the handoff commands.


## Revised development requirements

Domain tables use single int IDENTITY primary keys and native RowVersion columns. Identity retains its standard keys, joins and stamps. EF migrations/history are temporarily disabled: startup uses EnsureCreated plus seeding; --reset-database recreates the Development database. No old data or migration history is preserved. Business status/review history remains enabled.



Application-list paging is now implemented using database COUNT/Skip/Take (25 rows/page), with filter/ownership predicates and unique creation-date/ID ordering. Sorting controls remain optional.
