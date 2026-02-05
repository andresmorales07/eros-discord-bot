using ErosTTS.Bot.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ErosTTS.Bot.Tests.Data;

/// <summary>
/// Base class for EF Core tests using SQLite in-memory database.
/// Each test gets a fresh database with the schema created.
/// </summary>
public abstract class EfTestBase : IDisposable
{
    private readonly SqliteConnection _connection;
    protected readonly IDbContextFactory<ErosTtsDbContext> Factory;

    protected EfTestBase()
    {
        // SQLite in-memory requires keeping the connection open
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ErosTtsDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Create schema
        using var db = new ErosTtsDbContext(options);
        db.Database.EnsureCreated();

        Factory = new TestDbContextFactory(options);
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
