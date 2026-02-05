using ErosTTS.Bot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ErosTTS.Bot.Data;

/// <summary>
/// Entity Framework Core database context for the ErosTTS bot.
/// </summary>
public sealed class ErosTtsDbContext : DbContext
{
    public DbSet<GuildTtsConfigurationEntity> GuildConfigurations => Set<GuildTtsConfigurationEntity>();
    public DbSet<GuildCharacterStateEntity> GuildCharacterStates => Set<GuildCharacterStateEntity>();
    public DbSet<ConversationMessageEntity> ConversationMessages => Set<ConversationMessageEntity>();

    public ErosTtsDbContext(DbContextOptions<ErosTtsDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GuildTtsConfigurationEntity>(entity =>
        {
            entity.ToTable("GuildConfigurations");
            entity.HasKey(e => e.GuildId);
            entity.Property(e => e.GuildId).ValueGeneratedNever();
            entity.Property(e => e.VoiceId).HasMaxLength(100);
        });

        modelBuilder.Entity<GuildCharacterStateEntity>(entity =>
        {
            entity.ToTable("GuildCharacterStates");
            entity.HasKey(e => e.GuildId);
            entity.Property(e => e.GuildId).ValueGeneratedNever();
            entity.HasMany(e => e.ConversationHistory)
                  .WithOne(e => e.GuildCharacterState)
                  .HasForeignKey(e => e.GuildId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConversationMessageEntity>(entity =>
        {
            entity.ToTable("ConversationMessages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Role).HasMaxLength(20);
            entity.HasIndex(e => e.GuildId);
        });
    }
}
