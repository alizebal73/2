using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed class GameNetDbContext(DbContextOptions<GameNetDbContext> options) : DbContext(options)
{
    public DbSet<StationEntity> Stations => Set<StationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var station = modelBuilder.Entity<StationEntity>();
        station.HasKey(item => item.Id);
        station.HasIndex(item => item.Name).IsUnique();
        station.Property(item => item.Name).HasMaxLength(80).IsRequired();
        station.Property(item => item.Zone).HasMaxLength(30).IsRequired();
        station.Property(item => item.Type).HasMaxLength(80).IsRequired();
        station.Property(item => item.State).HasMaxLength(20).IsRequired();
    }
}