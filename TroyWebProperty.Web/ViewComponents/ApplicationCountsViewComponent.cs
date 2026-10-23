using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TroyWebProperty.Core;
using TroyWebProperty.Infrastructure.Data;
using TroyWebProperty.Web.Models;

namespace TroyWebProperty.Web.ViewComponents;

public sealed class ApplicationCountsViewComponent(ApplicationDbContext db) : ViewComponent
{

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var actor = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var manager = HttpContext.User.IsInRole(AppRoles.PropertyManager);
        var query = db.Applications.AsQueryable();

        if (!manager)
        {
            query = query.Where(x => x.Applicant.UserId == actor);
        }

        return View(await query.GroupBy(static x => x.Status.Code).Select(static x => new StatusCount(x.Key, x.Count()))
            .ToListAsync(HttpContext.RequestAborted));
    }

}