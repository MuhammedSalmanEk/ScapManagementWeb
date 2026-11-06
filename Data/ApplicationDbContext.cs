using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ScrapManagement.Web.Infrastructure;
using ScrapManagement.Web.Models;

namespace ScrapManagement.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ITenantProvider _tenant;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantProvider tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    // Read by the global query filters below. EF turns it into a query parameter per context instance.
    private int? CurrentShopId => _tenant.ShopId;

    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<SubscriptionEvent> SubscriptionEvents => Set<SubscriptionEvent>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<PurchaseBill> PurchaseBills => Set<PurchaseBill>();
    public DbSet<PurchaseBillItem> PurchaseBillItems => Set<PurchaseBillItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<InvoiceSequence> InvoiceSequences => Set<InvoiceSequence>();
    public DbSet<Payment> Payments => Set<Payment>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<ApplicationUser>(e =>
        {
            e.HasOne(u => u.Shop).WithMany().HasForeignKey(u => u.ShopId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(u => u.ShopId);
        });

        b.Entity<SubscriptionPlan>(e =>
        {
            e.Property(p => p.OfferYearly).HasPrecision(18, 2);
            e.Property(p => p.OfferMonthly).HasPrecision(18, 2);
        });

        b.Entity<Shop>(e =>
        {
            e.HasOne(s => s.Plan).WithMany().HasForeignKey(s => s.PlanId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(s => s.Status);
        });

        b.Entity<SubscriptionEvent>(e =>
        {
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.HasOne(x => x.Shop).WithMany().HasForeignKey(x => x.ShopId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Category>(e =>
        {
            e.HasIndex(c => new { c.ShopId, c.Name }).IsUnique();
            e.HasQueryFilter(c => c.ShopId == CurrentShopId);
        });

        b.Entity<Product>(e =>
        {
            e.Property(p => p.DefaultRatePerKg).HasPrecision(18, 2);
            e.Property(p => p.StockKg).HasPrecision(18, 3);
            e.HasIndex(p => new { p.ShopId, p.Name }).IsUnique();
            e.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.SetNull);
            e.HasQueryFilter(p => p.ShopId == CurrentShopId);
            e.Property(p => p.DefaultRatePerKg).HasPrecision(18, 2);
            e.Property(p => p.WholesaleRatePerKg).HasPrecision(18, 2);
        });

        b.Entity<PurchaseBill>(e =>
        {
            e.Property(x => x.TotalKg).HasPrecision(18, 3);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            // Invoice numbers are unique per shop (NULLs for drafts are allowed multiple times in PostgreSQL).
            e.HasIndex(x => new { x.ShopId, x.InvoiceNumber }).IsUnique();
            e.HasIndex(x => new { x.ShopId, x.BillDate });
            e.HasIndex(x => new { x.ShopId, x.Status });
            e.HasQueryFilter(x => x.ShopId == CurrentShopId);
        });

        b.Entity<PurchaseBillItem>(e =>
        {
            e.Property(x => x.QuantityKg).HasPrecision(18, 3);
            e.Property(x => x.RatePerKg).HasPrecision(18, 2);
            e.Property(x => x.LineTotal).HasPrecision(18, 2);
            e.HasOne(x => x.PurchaseBill).WithMany(x => x.Items).HasForeignKey(x => x.PurchaseBillId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => x.ShopId == CurrentShopId);
        });

        b.Entity<StockMovement>(e =>
        {
            e.Property(x => x.QuantityKg).HasPrecision(18, 3);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            // A bill line can add stock exactly once, enforced by the database.
            e.HasIndex(x => x.PurchaseBillItemId).IsUnique();
            e.HasIndex(x => new { x.ShopId, x.ProductId });
            e.HasQueryFilter(x => x.ShopId == CurrentShopId);
        });

        b.Entity<InvoiceSequence>(e =>
        {
            e.HasKey(x => new { x.ShopId, x.Year });
        });
        // inside OnModelCreating
        b.Entity<Payment>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.CreditedAmount).HasPrecision(18, 2);
            e.HasOne(x => x.Shop).WithMany().HasForeignKey(x => x.ShopId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.RazorpayOrderId).IsUnique();
            e.HasIndex(x => new { x.ShopId, x.Status });
        });
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTenant();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>Stamps new rows with the current shop and blocks writes into another shop.</summary>
    private void ApplyTenant()
    {
        var current = _tenant.ShopId;
        if (current is null) return;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>().Where(e => e.State == EntityState.Added))
        {
            if (entry.Entity.ShopId == 0)
                entry.Entity.ShopId = current.Value;
            else if (entry.Entity.ShopId != current.Value)
                throw new InvalidOperationException("Cross-shop write blocked.");
        }
    }
}
