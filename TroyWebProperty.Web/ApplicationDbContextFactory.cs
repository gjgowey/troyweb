using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TroyWebProperty.Infrastructure.Data;

namespace TroyWebProperty.Web;

// Allows schema script generation without running the application or needing a live server.
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", true)
            .AddUserSecrets<Program>(true).AddEnvironmentVariables().Build();
        var connection = configuration.GetConnectionString("DefaultConnection")
                         ??
                         "Server=lpc:.\\s2025;Database=RentalAssessment;Integrated Security=True;TrustServerCertificate=True;Encrypt=True";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        options.ConfigureRentalDatabase(connection);

        return new ApplicationDbContext(options.Options);
    }

}