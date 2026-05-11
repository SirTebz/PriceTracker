using PriceIntelligence.Domain.Entities;

namespace PriceIntelligence.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<User> CreateAsync(User user);
    Task UpdateAsync(User user);
}

public interface IProductRepository
{
    Task<(List<Product> Items, int Total)> SearchAsync(
        string? query, string? category, string? brand,
        decimal? minPrice, decimal? maxPrice, int page, int pageSize);
    Task<Product?> GetByIdAsync(Guid id);
    Task<Product?> GetByIdWithListingsAsync(Guid id);
    Task<Product> CreateAsync(Product product);
    Task UpdateAsync(Product product);
}

public interface IProductListingRepository
{
    Task<List<ProductListing>> GetByProductIdAsync(Guid productId);
    Task<ProductListing?> GetByUrlAsync(int retailerId, string url);
    Task<ProductListing> CreateAsync(ProductListing listing);
    Task UpdateAsync(ProductListing listing);
}

public interface IPriceHistoryRepository
{
    Task AddAsync(PriceHistory history);
    Task<List<PriceHistory>> GetByProductIdAsync(Guid productId, int days);
    Task<PriceHistory?> GetLowestEverAsync(Guid listingId);
}

public interface IWatchlistRepository
{
    Task<List<Watchlist>> GetByUserIdAsync(Guid userId);
    Task<Watchlist?> GetByIdAsync(Guid id, Guid userId);
    Task<Watchlist> CreateAsync(Watchlist watchlist);
    Task UpdateAsync(Watchlist watchlist);
    Task DeleteAsync(Guid id);
    Task AddItemAsync(WatchlistItem item);
    Task RemoveItemAsync(Guid watchlistId, Guid productId);
    Task<bool> ItemExistsAsync(Guid watchlistId, Guid productId);
}

public interface IAlertRepository
{
    Task<List<Alert>> GetByUserIdAsync(Guid userId);
    Task<Alert?> GetByIdAsync(Guid id);
    Task<List<Alert>> GetActiveAlertsAsync();
    Task<Alert> CreateAsync(Alert alert);
    Task UpdateAsync(Alert alert);
    Task DeleteAsync(Guid id);
}

public interface IRetailerRepository
{
    Task<List<Retailer>> GetAllAsync();
    Task<Retailer?> GetByIdAsync(int id);
    Task UpdateAsync(Retailer retailer);
}

public interface IScraperJobRepository
{
    Task<List<ScraperJob>> GetRecentAsync(int count);
    Task<ScraperJob> CreateAsync(ScraperJob job);
    Task UpdateAsync(ScraperJob job);
}