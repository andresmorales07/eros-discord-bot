using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ErosTTS.Bot.Data;

/// <summary>
/// Design-time factory for EF Core tooling (dotnet ef migrations).
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ErosTtsDbContext>
{
    public ErosTtsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ErosTtsDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;

        return new ErosTtsDbContext(options);
    }
}
