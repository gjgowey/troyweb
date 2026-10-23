using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TroyWebProperty.Core;
using TroyWebProperty.Core.Entities;
using TroyWebProperty.Infrastructure;
using TroyWebProperty.Infrastructure.Data;
using TroyWebProperty.Web.Models;

namespace TroyWebProperty.Web.Controllers;

[Authorize(Roles = AppRoles.Applicant + "," + AppRoles.PropertyManager)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ApplicationsController(ApplicationDbContext db, ApplicationService service) : Controller
{

    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier)
                            ?? throw new InvalidOperationException("The authenticated user has no name identifier.");

    private string ActorName => User.Identity?.Name ?? Actor;
    private bool Manager => User.IsInRole(AppRoles.PropertyManager);

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (string.IsNullOrWhiteSpace(User.FindFirstValue(ClaimTypes.NameIdentifier)))
        {
            context.Result = Forbid();

            return;
        }

        base.OnActionExecuting(context);
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? status, int? propertyId, int page = 1,
        CancellationToken cancellationToken = default)
    {
        var query = db.Applications.AsNoTracking().AsQueryable();

        if (!Manager)
        {
            var actor = Actor;
            query = query.Where(x => x.Applicant.UserId == actor);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusCode = status;
            query = query.Where(x => x.Status.Code == statusCode);
        }

        if (propertyId.HasValue)
        {
            var selectedPropertyId = propertyId.Value;
            query = query.Where(x => x.Unit.PropertyId == selectedPropertyId);
        }

        const int pageSize = 25;
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var rows = await query.OrderByDescending(static x => x.CreatedAtUtc).ThenByDescending(static x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(static x => new ApplicationRow(
                x.Id,
                (x.Title == null || x.Title == "" ? "" : x.Title + " ") + x.FirstName + " " +
                (x.MiddleInitial == null || x.MiddleInitial == "" ? "" : x.MiddleInitial + ". ") + x.LastName,
                x.Unit.Property.Name, x.Unit.Number, x.Status.Code, x.CreatedAtUtc)).ToListAsync(cancellationToken);

        return View(new ApplicationListModel
        {
            Status = status, PropertyId = propertyId, Rows = rows,
            Page = page, PageSize = pageSize, TotalCount = totalCount,
            Properties = await db.Properties.OrderBy(static x => x.Name)
                .Select(static x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(cancellationToken)
        });
    }

    [Authorize(Roles = AppRoles.Applicant)]
    [HttpPost]
    public async Task<IActionResult> Start(int unitId)
    {
        try
        {
            return RedirectToAction(nameof(Edit),
                new { id = await service.StartAsync(unitId, Actor, ActorName) });
        }
        catch (RuleException error)
        {
            TempData["Notice"] = error.Message;

            return RedirectToAction("Index", "Units");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, int step = 1)
    {
        var row = await Allowed(id);

        if (row is null)
        {
            return NotFound();
        }

        return View(await Model(row, step));
    }

    [Authorize(Roles = AppRoles.Applicant)]
    [HttpPost]
    public async Task<IActionResult> Edit(ApplicationEditModel model, string command)
    {
        var row = await Allowed(model.Id);

        if (row is null)
        {
            return NotFound();
        }

        if (!ApplicationRules.CanEdit(row.StatusCode))
        {
            return Conflict("This application is read-only.");
        }

        if (command == "Back")
        {
            return RedirectToAction(nameof(Edit), new { id = model.Id, step = Math.Max(1, model.Step - 1) });
        }

        if (model.Step != 1)
        {
            foreach (var key in ModelState.Keys.Where(static x => x.StartsWith("Info.", StringComparison.Ordinal))
                         .ToArray())
                ModelState.Remove(key);
        }

        if (command is not ("Continue" or "Submit"))
        {
            return BadRequest();
        }

        if (model.Step is < 1 or > 3 || command == "Submit" && model.Step != 3 ||
            command == "Continue" && model.Step == 3)
        {
            return BadRequest();
        }

        if (ModelState.IsValid)
        {
            try
            {
                if (command == "Submit")
                {
                    await service.SubmitAsync(model.Id, Convert.FromBase64String(model.RowVersion), Actor,
                        ActorName);
                }
                else if (model.Step == 1)
                {
                    await service.SaveInformationAsync(model.Id, Convert.FromBase64String(model.RowVersion), Actor,
                        model.Info);
                }
                else
                {
                    await service.SaveHistoryAsync(model.Id, Convert.FromBase64String(model.RowVersion), Actor);
                }

                TempData["Notice"] = command == "Submit" ? "Application submitted." : "Section saved.";

                return RedirectToAction(nameof(Edit),
                    new { id = model.Id, step = command == "Submit" ? 3 : model.Step + 1 });
            }
            catch (RuleException error)
            {
                ModelState.AddModelError("", error.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError("", "Application changed. Reload before saving.");
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        var output = await Model(row, model.Step);

        // Preserve the invalid section's posted values; never persist them.
        if (model.Step == 1)
        {
            output.Info = model.Info;
        }

        output.RowVersion = model.RowVersion;

        return View(output);
    }

    [Authorize(Roles = AppRoles.Applicant)]
    [HttpPost]
    public async Task<IActionResult> Withdraw(int id, byte[] rowVersion)
    {
        if (!ModelState.IsValid || rowVersion is null || rowVersion.Length != 8)
        {
            return BadRequest("Invalid rowversion.");
        }

        if (await Allowed(id) is null)
        {
            return NotFound();
        }

        try
        {
            await service.WithdrawAsync(id, rowVersion, Actor, ActorName);
            TempData["Notice"] = "Application withdrawn.";
        }
        catch (RuleException error)
        {
            TempData["Notice"] = error.Message;
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [Authorize(Roles = AppRoles.Applicant)]
    [HttpGet]
    public async Task<IActionResult> Residence(int applicationId, int id = 0, bool delete = false)
    {
        var row = await Allowed(applicationId);

        if (row is null)
        {
            return NotFound();
        }

        if (!ApplicationRules.CanEdit(row.StatusCode))
        {
            return Conflict("Application is read-only.");
        }

        var residence = row.Residences.SingleOrDefault(x => x.Id == id);

        if (id != 0 && residence is null)
        {
            return NotFound();
        }

        return PartialView("_ResidenceModal", new ResidenceForm
        {
            ApplicationId = applicationId, Id = id, RowVersion = Convert.ToBase64String(row.RowVersion),
            Delete = delete,
            Address = residence?.Address ?? "", LandlordName = residence?.LandlordName ?? "",
            LandlordPhone = residence?.LandlordPhone ?? "",
            MoveIn = residence?.MoveIn ?? service.Today.AddYears(-2),
            MoveOut = residence?.MoveOut ?? service.Today.AddYears(-1)
        });
    }

    [Authorize(Roles = AppRoles.Applicant)]
    [HttpPost]
    public async Task<IActionResult> Residence(ResidenceForm model)
    {
        var row = await Allowed(model.ApplicationId);

        if (row is null)
        {
            return NotFound();
        }

        if (!ApplicationRules.CanEdit(row.StatusCode))
        {
            return Conflict("Application is read-only.");
        }

        if (model.Delete)
        {
            ModelState.Clear();
        }

        if (ModelState.IsValid)
        {
            try
            {
                await service.SaveResidenceAsync(model.ApplicationId, Convert.FromBase64String(model.RowVersion), Actor,
                    new Residence
                    {
                        Id = model.Id, Address = model.Address, LandlordName = model.LandlordName,
                        LandlordPhone = model.LandlordPhone, MoveIn = model.MoveIn, MoveOut = model.MoveOut
                    }, model.Delete);

                return Json(new
                {
                    success = true, refreshUrl = Url.Action(nameof(Edit), new { id = model.ApplicationId, step = 2 }),
                    target = "#application-shell"
                });
            }
            catch (RuleException error)
            {
                ModelState.AddModelError("", error.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError("", "Application changed. Close this modal and reload.");
            }
        }

        Response.StatusCode = 422;

        return PartialView("_ResidenceModal", model);
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    [HttpGet]
    public async Task<IActionResult> Review(int id)
    {
        var row = await Allowed(id);

        if (row is null)
        {
            return NotFound();
        }

        if (row.StatusCode != "Submitted")
        {
            return Conflict("Only submitted applications can be reviewed.");
        }

        return PartialView("_ReviewModal",
            new ReviewForm { Id = id, RowVersion = Convert.ToBase64String(row.RowVersion), StartDate = service.Today });
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    [HttpPost]
    public async Task<IActionResult> Review(ReviewForm model)
    {
        if (await Allowed(model.Id) is null)
        {
            return NotFound();
        }

        if (model.Outcome is "Returned" or "Denied" && string.IsNullOrWhiteSpace(model.Comment))
        {
            ModelState.AddModelError(nameof(model.Comment), "A comment is required for Return and Deny.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                await service.ReviewAsync(model.Id, Convert.FromBase64String(model.RowVersion), Actor,
                    ActorName, model.Outcome,
                    model.Comment, model.StartDate);

                return Json(new
                {
                    success = true, refreshUrl = Url.Action(nameof(Edit), new { id = model.Id, step = 3 }),
                    target = "#application-shell"
                });
            }
            catch (RuleException error)
            {
                ModelState.AddModelError("", error.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError("", "Application changed. Close and reload before reviewing.");
            }
        }

        Response.StatusCode = 422;

        return PartialView("_ReviewModal", model);
    }

    private Task<RentalApplication?> Allowed(int id)
    {
        return db.Applications.AsNoTracking().AsSplitQuery()
            .Include(static x => x.Status).Include(static x => x.Applicant).Include(static x => x.Unit)
            .ThenInclude(static x => x.Property)
            .Include(static x => x.Residences).Include(static x => x.Changes)
            .SingleOrDefaultAsync(x => x.Id == id && (Manager || x.Applicant.UserId == Actor),
                HttpContext.RequestAborted);
    }

    private async Task<ApplicationEditModel> Model(RentalApplication row, int step)
    {
        return new ApplicationEditModel
        {
            Id = row.Id, RowVersion = Convert.ToBase64String(row.RowVersion), Step = Math.Clamp(step, 1, 3),
            Status = row.StatusCode,
            ReadOnly = Manager || !ApplicationRules.CanEdit(row.StatusCode), Manager = Manager,
            CanSubmit = row.InformationSaved && row.HistorySaved && ApplicationRules.CanEdit(row.StatusCode),
            CanWithdraw = !Manager && ApplicationRules.CanWithdraw(row.StatusCode),
            UnitDescription = $"{row.Unit.Property.Name} · Unit {row.Unit.Number}",
            Info = new ApplicantInformation
            {
                FirstName = row.FirstName, LastName = row.LastName, MiddleInitial = row.MiddleInitial,
                Title = row.Title,
                Email = row.Email, Phone = row.Phone, CurrentAddress = row.CurrentAddress
            },
            Residences = row.Residences.OrderByDescending(static x => x.MoveIn).ToList(),
            Changes = Manager ? row.Changes.OrderBy(static x => x.ChangedAtUtc).ToList() : [],
            ReturnComment = row.StatusCode == "Returned"
                ? row.Changes.Where(static x => x.ToStatus == "Returned").OrderByDescending(static x => x.ChangedAtUtc)
                    .FirstOrDefault()
                    ?.Comment
                : null,
            Lease = await db.Leases.AsNoTracking()
                .SingleOrDefaultAsync(x => x.RentalApplicationId == row.Id, HttpContext.RequestAborted)
        };
    }

}