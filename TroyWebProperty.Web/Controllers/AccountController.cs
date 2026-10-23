using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TroyWebProperty.Core;
using TroyWebProperty.Core.Entities;
using TroyWebProperty.Infrastructure.Data;
using TroyWebProperty.Web.Models;

namespace TroyWebProperty.Web.Controllers;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AccountController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    ApplicationDbContext db,
    ILogger<AccountController> logger) : Controller
{

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        return View(new RegisterViewModel { ReturnUrl = LocalUrl(returnUrl) });
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        model.ReturnUrl = LocalUrl(model.ReturnUrl);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();
        var user = new ApplicationUser { UserName = email, Email = email };

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var created = await users.CreateAsync(user, model.Password);

            if (!created.Succeeded)
            {
                foreach (var error in created.Errors)
                    ModelState.AddModelError("", error.Description);

                return View(model);
            }

            // Assessment explicitly permits either of these two roles at sign-up.
            if (model.Role is not (AppRoles.Applicant or AppRoles.PropertyManager))
            {
                return BadRequest();
            }

            var assigned = await users.AddToRoleAsync(user, model.Role);

            if (!assigned.Succeeded)
            {
                logger.LogError("Registration role assignment failed for {UserId}: {Errors}",
                    user.Id, string.Join("; ", assigned.Errors.Select(static x => x.Description)));
                ModelState.AddModelError("", "Registration could not be completed. Please try again.");

                return View(model); // Disposal rolls back account creation too.
            }

            if (model.Role == AppRoles.Applicant)
            {
                db.Applicants.Add(new Applicant
                {
                    UserId = user.Id, FirstName = model.FirstName.Trim(), LastName = model.LastName.Trim(),
                    MiddleInitial = model.MiddleInitial?.Trim().ToUpperInvariant(), Title = model.Title,
                    Email = email
                });
            }
            else
            {
                db.PropertyManagers.Add(new PropertyManager
                {
                    UserId = user.Id, FirstName = model.FirstName.Trim(), LastName = model.LastName.Trim(),
                    MiddleInitial = model.MiddleInitial?.Trim().ToUpperInvariant(), Title = model.Title,
                    Email = email
                });
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        // Role membership is committed before generating the authentication cookie.
        await signIn.SignInAsync(user, false);

        return LocalRedirect(LocalUrl(model.ReturnUrl));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = LocalUrl(returnUrl) });
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        model.ReturnUrl = LocalUrl(model.ReturnUrl);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await signIn.PasswordSignInAsync(
            model.Email.Trim(), model.Password, model.RememberMe, true);

        if (result.Succeeded)
        {
            return LocalRedirect(LocalUrl(model.ReturnUrl));
        }

        ModelState.AddModelError("", "Unable to sign in. Check your credentials or try again later.");

        return View(model);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();

        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;

        return View();
    }

    private string LocalUrl(string? value)
    {
        if (value is not null && Url.IsLocalUrl(value))
        {
            return value;
        }

        return Url.Action("Index", "Applications")
               ?? throw new InvalidOperationException("Unable to generate the applications URL.");
    }

}