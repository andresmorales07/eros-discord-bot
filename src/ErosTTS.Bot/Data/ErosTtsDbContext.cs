using ErosTTS.Bot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ErosTTS.Bot.Data;

/// <summary>
/// Entity Framework Core database context for the ErosTTS bot.
/// </summary>
public sealed class ErosTtsDbContext : DbContext
{
    public DbSet<GuildTtsConfigurationEntity> GuildConfigurations => Set<GuildTtsConfigurationEntity>();
    public DbSet<NpcEntity> Npcs => Set<NpcEntity>();
    public DbSet<GuildNpcSettingsEntity> GuildNpcSettings => Set<GuildNpcSettingsEntity>();
    public DbSet<NpcConversationMessageEntity> NpcConversationMessages => Set<NpcConversationMessageEntity>();

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

        modelBuilder.Entity<NpcEntity>(entity =>
        {
            entity.ToTable("Npcs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.VoiceId).HasMaxLength(100);
            entity.HasIndex(e => new { e.GuildId, e.Name }).IsUnique();
            entity.HasMany(e => e.ConversationMessages)
                  .WithOne(e => e.Npc)
                  .HasForeignKey(e => e.NpcId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<GuildNpcSettingsEntity>(entity =>
        {
            entity.ToTable("GuildNpcSettings");
            entity.HasKey(e => e.GuildId);
            entity.Property(e => e.GuildId).ValueGeneratedNever();
            entity.HasOne(e => e.ActiveNpc)
                  .WithMany()
                  .HasForeignKey(e => e.ActiveNpcId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<NpcConversationMessageEntity>(entity =>
        {
            entity.ToTable("NpcConversationMessages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Role).HasMaxLength(20);
            entity.Property(e => e.NpcName).HasMaxLength(100);
            entity.HasIndex(e => e.GuildId);
            entity.HasIndex(e => e.NpcId);
        });
    }
}
