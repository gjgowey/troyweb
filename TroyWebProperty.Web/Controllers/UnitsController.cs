using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TroyWebProperty.Core;
using TroyWebProperty.Core.Entities;
using TroyWebProperty.Infrastructure;
using TroyWebProperty.Infrastructure.Data;
using TroyWebProperty.Web.Models;

namespace TroyWebProperty.Web.Controllers;

public sealed class UnitsController(ApplicationDbContext db, ApplicationService service) : Controller
{

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var today = service.Today;

        return View(await new RentalRepository(db).AvailableUnits(today).AsNoTracking()
            .OrderBy(static u => u.Property.Name).ThenBy(static u => u.Number)
            .Select(static u => new UnitRow(u.Id, u.PropertyId, u.Property.Name, u.Property.Address, u.Number,
                u.Bedrooms,
                u.MonthlyRent, u.UnitType.Name)).ToListAsync(cancellationToken));
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Edit(int id = 0, int propertyId = 0, bool delete = false)
    {
        var unit = id == 0 ? null : await db.Units.FindAsync(id);

        if (id != 0 && unit is null)
        {
            return NotFound();
        }

        var model = new UnitForm
        {
            Id = id, RowVersion = unit is null ? null : Convert.ToBase64String(unit.RowVersion),
            PropertyId = unit?.PropertyId ?? propertyId, Delete = delete,
            Number = unit?.Number ?? "", Bedrooms = unit?.Bedrooms ?? 0, MonthlyRent = unit?.MonthlyRent ?? 1000,
            UnitTypeId = unit?.UnitTypeId ?? 0
        };
        await Types(model, unit?.UnitTypeId);

        return PartialView("_Edit", model);
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    [HttpPost]
    public async Task<IActionResult> Edit(UnitForm model)
    {
        var unit = model.Id == 0 ? null : await db.Units.FindAsync(model.Id);

        if (model.Id != 0 && unit is null)
        {
            return NotFound();
        }

        if (model.Delete)
        {
            ModelState.Clear();
        }

        if (unit is not null && !ConcurrencyTokens.Matches(unit.RowVersion, model.RowVersion))
        {
            ModelState.AddModelError("", "This unit changed. Close this form and reload.");
        }

        if (ModelState.IsValid)
        {
            if (model.Delete)
            {
                if (unit is null)
                {
                    return NotFound();
                }

                if (await db.Applications.AnyAsync(x => x.UnitId == unit.Id) ||
                    await db.Leases.AnyAsync(x => x.UnitId == unit.Id))
                {
                    ModelState.AddModelError("",
                        "A unit with applications or leases cannot be removed; its history must be retained.");
                }
                else
                {
                    db.Remove(unit);
                }
            }
            else
            {
                var type = await db.UnitTypes.FindAsync(model.UnitTypeId);

                if (type is null)
                {
                    ModelState.AddModelError(nameof(model.UnitTypeId), "Choose a valid unit type.");
                }
                else
                {
                    try
                    {
                        ApplicationRules.UnitType(type.IsActive, model.UnitTypeId, unit?.UnitTypeId);
                    }
                    catch (RuleException error)
                    {
                        ModelState.AddModelError(nameof(model.UnitTypeId), error.Message);
                    }
                }

                if (!await db.Properties.AnyAsync(x => x.Id == model.PropertyId))
                {
                    ModelState.AddModelError("", "Choose an existing property.");
                }

                if (await db.Units.AnyAsync(x =>
                        x.PropertyId == model.PropertyId && x.Number == model.Number.Trim() && x.Id != model.Id))
                {
                    ModelState.AddModelError(nameof(model.Number), "Unit number already exists in this property.");
                }

                if (ModelState.IsValid)
                {
                    if (unit is null)
                    {
                        unit = new Unit();
                        db.Add(unit);
                    }

                    unit.PropertyId = model.PropertyId;
                    unit.Number = model.Number.Trim();
                    unit.Bedrooms = model.Bedrooms;
                    unit.MonthlyRent = model.MonthlyRent;
                    unit.UnitTypeId = model.UnitTypeId;
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    await db.SaveChangesAsync();

                    return Json(new { success = true });
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("",
                        "The unit is in use or its number already exists. Refresh and try again.");
                }
            }
        }

        await Types(model, unit?.UnitTypeId);
        Response.StatusCode = 422;

        return PartialView("_Edit", model);
    }

    private async Task Types(UnitForm model, int? current)
    {
        model.Types = await db.UnitTypes
            .Where(x => x.IsActive || x.Id == current).OrderBy(static x => x.Name)
            .Select(static x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
    }

}