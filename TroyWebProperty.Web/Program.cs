using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TroyWebProperty.Infrastructure;
using TroyWebProperty.Infrastructure.Data;

var resetDatabase = args.Contains("--reset-database", StringComparer.OrdinalIgnoreCase);
var builder =
    WebApplication.CreateBuilder(args
        .Where(static x => !x.Equals("--reset-database", StringComparison.OrdinalIgnoreCase))
        .ToArray());

// Add services to the container.
// Cookie-authenticated MVC forms validate unsafe methods by default, including future controllers.
builder.Services.AddControllersWithViews(static options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<RentalRepository>();
builder.Services.AddScoped<ApplicationService>();
builder.Services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
var connection = builder.Configuration.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException("DefaultConnection is required.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.ConfigureRentalDatabase(connection));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(static options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 1;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(static options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

// Every environment: create the schema directly from the model, then seed before accepting requests.
// Schema changes require a deliberate drop/recreate; EF migrations are temporarily disabled.
await using (var scope = app.Services.CreateAsyncScope())
{
    if (resetDatabase)
    {
        if (!app.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("Database reset is only available in Development.");
        }

        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureDeletedAsync();
    }

    await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();

    if (app.Environment.IsDevelopment())
    {
        await DatabaseSeeder.SetDevelopmentPasswordsAsync(
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    app.Logger.LogInformation("Schema created and sample rental data seeded (migrations disabled).");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// wwwroot contains public assets only. Execute them at routing, before Identity/authorization.
app.MapStaticAssets().ShortCircuit();

app.MapControllerRoute(
        "default",
        "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

