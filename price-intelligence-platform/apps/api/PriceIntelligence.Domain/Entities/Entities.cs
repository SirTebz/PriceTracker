using PriceIntelligence.Domain.Common;
using PriceIntelligence.Domain.Enums;

namespace PriceIntelligence.Domain.Entities;

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;
    public ICollection<Watchlist> Watchlists { get; set; } = new List<Watchlist>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

public class Retailer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Country { get; set; } = "ZA";
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ProductListing> Listings { get; set; } = new List<ProductListing>();
}

public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Category { get; set; }
    public string? SubCategory { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? Barcode { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ProductListing> Listings { get; set; } = new List<ProductListing>();
    public ICollection<WatchlistItem> WatchlistItems { get; set; } = new List<WatchlistItem>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    public ICollection<ProductMatch> Matches { get; set; } = new List<ProductMatch>();
}

public class ProductListing : BaseEntity
{
    public Guid ProductId { get; set; }
    public int RetailerId { get; set; }
    public string? ExternalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string Currency { get; set; } = "ZAR";
    public StockStatus StockStatus { get; set; } = StockStatus.Unknown;
    public string ProductUrl { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTime LastScrapedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public Product Product { get; set; } = null!;
    public Retailer Retailer { get; set; } = null!;
    public ICollection<PriceHistory> PriceHistory { get; set; } = new List<PriceHistory>();
}

public class PriceHistory
{
    public long Id { get; set; }
    public Guid ProductListingId { get; set; }
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public StockStatus StockStatus { get; set; } = StockStatus.Unknown;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public ProductListing ProductListing { get; set; } = null!;
}

public class ProductMatch
{
    public long Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid ProductListingId { get; set; }
    public double SimilarityScore { get; set; }
    public string MatchMethod { get; set; } = "AI";
    public DateTime? ConfirmedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Product Product { get; set; } = null!;
    public ProductListing ProductListing { get; set; } = null!;
}

public class Watchlist : BaseEntity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public User User { get; set; } = null!;
    public ICollection<WatchlistItem> Items { get; set; } = new List<WatchlistItem>();
}

public class WatchlistItem
{
    public long Id { get; set; }
    public Guid WatchlistId { get; set; }
    public Guid ProductId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public Watchlist Watchlist { get; set; } = null!;
    public Product Product { get; set; } = null!;
}

public class Alert : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public AlertConditionType ConditionType { get; set; }
    public decimal? ThresholdValue { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastTriggeredAt { get; set; }
    public User User { get; set; } = null!;
    public Product Product { get; set; } = null!;
}

public class Notification
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? AlertId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "PriceAlert";
    public bool IsRead { get; set; }
    public bool IsSent { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}

public class ScraperJob
{
    public long Id { get; set; }
    public int RetailerId { get; set; }
    public string Status { get; set; } = "Pending";
    public int ItemsScraped { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Retailer Retailer { get; set; } = null!;
}