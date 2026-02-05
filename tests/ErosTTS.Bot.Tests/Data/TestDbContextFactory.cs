using ErosTTS.Bot.Data;
using Microsoft.EntityFrameworkCore;

namespace ErosTTS.Bot.Tests.Data;

/// <summary>
/// Test implementation of IDbContextFactory that creates contexts with pre-configured options.
/// </summary>
internal sealed class TestDbContextFactory : IDbContextFactory<ErosTtsDbContext>
{
    private readonly DbContextOptions<ErosTtsDbContext> _options;

    public TestDbContextFactory(DbContextOptions<ErosTtsDbContext> options) => _options = options;

    public ErosTtsDbContext CreateDbContext() => new(_options);
}
