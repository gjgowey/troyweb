using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TroyWebProperty.Core;
using TroyWebProperty.Core.Entities;
using TroyWebProperty.Infrastructure.Data;
using TroyWebProperty.Web.Models;

namespace TroyWebProperty.Web.Controllers;

[Authorize(Roles = AppRoles.PropertyManager)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class PropertiesController(ApplicationDbContext db) : Controller
{

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var properties = await db.Properties.AsNoTracking().OrderBy(static x => x.Name)
            .Select(static x => new { x.Id, x.Name, x.Address }).ToListAsync(cancellationToken);
        var units = await db.Units.AsNoTracking().OrderBy(static u => u.Number)
            .Select(static u => new UnitRow(u.Id, u.PropertyId, u.Property.Name, u.Property.Address,
                u.Number, u.Bedrooms, u.MonthlyRent, u.UnitType.Name)).ToListAsync(cancellationToken);
        var groups = units.ToLookup(static x => x.PropertyId);

        return View(properties.Select(x => new PropertyRow(x.Id, x.Name, x.Address, groups[x.Id].ToList())).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id = 0, bool delete = false)
    {
        var row = id == 0 ? null : await db.Properties.FindAsync(id);

        if (id != 0 && row is null)
        {
            return NotFound();
        }

        return PartialView("_Edit",
            new PropertyForm
            {
                Id = id, RowVersion = row is null ? null : Convert.ToBase64String(row.RowVersion), Delete = delete,
                Name = row?.Name ?? "", Address = row?.Address ?? ""
            });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(PropertyForm model)
    {
        var row = model.Id == 0 ? null : await db.Properties.FindAsync(model.Id);

        if (model.Id != 0 && row is null)
        {
            return NotFound();
        }

        if (model.Delete)
        {
            ModelState.Clear();
        }

        if (row is not null && !ConcurrencyTokens.Matches(row.RowVersion, model.RowVersion))
        {
            ModelState.AddModelError("", "This property changed. Close this form and reload.");
        }

        if (ModelState.IsValid)
        {
            if (model.Delete)
            {
                if (row is null)
                {
                    return NotFound();
                }

                if (await db.Units.AnyAsync(x => x.PropertyId == row.Id))
                {
                    ModelState.AddModelError("", "Remove this property's units before removing the property.");
                }
                else
                {
                    db.Remove(row);
                }
            }
            else
            {
                if (row is null)
                {
                    var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    var manager = await db.PropertyManagers.SingleAsync(x => x.UserId == actor);
                    row = new Property { PropertyManagerId = manager.Id };
                    db.Add(row);
                }

                row.Name = model.Name.Trim();
                row.Address = model.Address.Trim();
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
                    ModelState.AddModelError("", "The property is in use or changed. Refresh and try again.");
                }
            }
        }

        Response.StatusCode = 422;

        return PartialView("_Edit", model);
    }

}