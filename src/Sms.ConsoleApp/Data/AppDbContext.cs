using Microsoft.EntityFrameworkCore;

namespace Sms.ConsoleApp.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MenuItemEntity> MenuItems => Set<MenuItemEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MenuItemEntity>(entity =>
        {
            entity.ToTable("menu_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Article).HasMaxLength(64);
            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.Price).HasPrecision(14, 2);
            entity.Property(e => e.FullPath).HasMaxLength(512);
        });
    }
}
