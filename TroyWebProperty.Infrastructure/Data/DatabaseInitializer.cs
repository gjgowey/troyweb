namespace TroyWebProperty.Infrastructure.Data;

public interface IDatabaseInitializer
{

    Task InitializeAsync();

}

// Schema lifecycle for every environment. No migrations or EF history table are used.
public sealed class DatabaseInitializer(ApplicationDbContext db) : IDatabaseInitializer
{

    public async Task InitializeAsync()
    {
        await db.Database.EnsureCreatedAsync();
        await DatabaseSeeder.SeedAsync(db);
    }

}