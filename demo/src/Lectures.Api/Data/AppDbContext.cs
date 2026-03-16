using Lectures.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Lectures.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CustomerName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.CustomerEmail).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Ignore(e => e.TotalPrice);
        });
    }
}
