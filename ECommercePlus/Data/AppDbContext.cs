using ECommercePlus.Domain;
using ECommercePlus.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ECommercePlus.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider timeProvider) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(ProductRules.NameMaxLength).IsRequired();
            entity.Property(p => p.Sku).HasMaxLength(ProductRules.SkuMaxLength).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(ProductRules.DescriptionMaxLength);
            entity.Property(p => p.Category).HasMaxLength(ProductRules.CategoryMaxLength).IsRequired();
            entity.Property(p => p.Price).HasConversion(MoneyConverters.ToCents);
            entity.Property(p => p.WeightKg).HasConversion(MoneyConverters.WeightToGrams);
            entity.Property(p => p.Version).IsConcurrencyToken();
            entity.HasIndex(p => p.Sku).IsUnique();
            entity.HasIndex(p => p.Name);
            entity.HasIndex(p => p.Category);
            entity.Ignore(p => p.InStock);
            entity.ToTable(t => t.HasCheckConstraint("CK_Products_Stock_NonNegative", "\"Stock\" >= 0"));
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(o => o.OrderNumber).HasMaxLength(32).IsRequired();
            entity.Property(o => o.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(o => o.CustomerEmail).HasMaxLength(254).IsRequired();
            entity.Property(o => o.ShippingAddress).HasMaxLength(500).IsRequired();
            entity.Property(o => o.CheckoutToken).HasMaxLength(64).IsRequired();
            entity.Property(o => o.PaymentReference).HasMaxLength(64);
            entity.Property(o => o.CardLast4).HasMaxLength(4);
            entity.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(o => o.Total).HasConversion(MoneyConverters.ToCents);
            entity.HasIndex(o => o.OrderNumber).IsUnique();
            entity.HasIndex(o => o.CheckoutToken).IsUnique();
            entity.HasIndex(o => o.CreatedAtUtc);
            entity.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.Ignore(o => o.ItemCount);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(i => i.ProductName).HasMaxLength(ProductRules.NameMaxLength).IsRequired();
            entity.Property(i => i.Sku).HasMaxLength(ProductRules.SkuMaxLength).IsRequired();
            entity.Property(i => i.UnitPrice).HasConversion(MoneyConverters.ToCents);
            entity.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.SetNull);
            entity.Ignore(i => i.LineTotal);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampProducts();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampProducts();
        return base.SaveChanges();
    }

    private void StampProducts()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var entry in ChangeTracker.Entries<Product>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = now;
                entry.Entity.Version = Guid.NewGuid();
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
                entry.Entity.Version = Guid.NewGuid();
            }
        }
    }
}
