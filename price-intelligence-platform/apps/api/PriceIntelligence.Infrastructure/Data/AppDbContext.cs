using Microsoft.EntityFrameworkCore;
using PriceIntelligence.Domain.Entities;

namespace PriceIntelligence.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Retailer> Retailers { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductListing> ProductListings { get; set; }
    public DbSet<PriceHistory> PriceHistories { get; set; }
    public DbSet<ProductMatch> ProductMatches { get; set; }
    public DbSet<Watchlist> Watchlists { get; set; }
    public DbSet<WatchlistItem> WatchlistItems { get; set; }
    public DbSet<Alert> Alerts { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<ScraperJob> ScraperJobs { get; set; }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Role).HasConversion<string>();
        });

        b.Entity<Retailer>(e => {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Product>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(500).IsRequired();
            e.HasIndex(x => x.Category);
            e.HasIndex(x => x.Brand);
        });

        b.Entity<ProductListing>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.OriginalPrice).HasPrecision(18, 2);
            e.Property(x => x.StockStatus).HasConversion<string>();
            e.HasIndex(x => new { x.RetailerId, x.ProductUrl }).IsUnique();
            e.HasOne(x => x.Product).WithMany(x => x.Listings).HasForeignKey(x => x.ProductId);
            e.HasOne(x => x.Retailer).WithMany(x => x.Listings).HasForeignKey(x => x.RetailerId);
        });

        b.Entity<PriceHistory>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.OriginalPrice).HasPrecision(18, 2);
            e.Property(x => x.StockStatus).HasConversion<string>();
            e.HasIndex(x => new { x.ProductListingId, x.RecordedAt });
            e.HasOne(x => x.ProductListing).WithMany(x => x.PriceHistory).HasForeignKey(x => x.ProductListingId);
        });

        b.Entity<ProductMatch>(e => {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ProductId, x.ProductListingId }).IsUnique();
            e.HasOne(x => x.Product).WithMany(x => x.Matches)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.ProductListing).WithMany()
            .HasForeignKey(x => x.ProductListingId)
            .OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<Watchlist>(e => {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.User).WithMany(x => x.Watchlists).HasForeignKey(x => x.UserId);
        });

        b.Entity<WatchlistItem>(e => {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.WatchlistId, x.ProductId }).IsUnique();
            e.HasOne(x => x.Watchlist).WithMany(x => x.Items).HasForeignKey(x => x.WatchlistId);
            e.HasOne(x => x.Product).WithMany(x => x.WatchlistItems).HasForeignKey(x => x.ProductId);
        });

        b.Entity<Alert>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.ConditionType).HasConversion<string>();
            e.Property(x => x.ThresholdValue).HasPrecision(18, 2);
            e.HasOne(x => x.User).WithMany(x => x.Alerts).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Product).WithMany(x => x.Alerts).HasForeignKey(x => x.ProductId);
        });

        b.Entity<Notification>(e => {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.User).WithMany(x => x.Notifications).HasForeignKey(x => x.UserId);
        });

        b.Entity<ScraperJob>(e => {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Retailer).WithMany().HasForeignKey(x => x.RetailerId);
        });
    }
}