using Microsoft.EntityFrameworkCore;

namespace TroyWebProperty.Infrastructure.Data;

public static class DatabaseConfiguration
{

    public static DbContextOptionsBuilder ConfigureRentalDatabase(this DbContextOptionsBuilder builder,
        string connectionString)
    {
        return builder.UseSqlServer(connectionString);
    }

}