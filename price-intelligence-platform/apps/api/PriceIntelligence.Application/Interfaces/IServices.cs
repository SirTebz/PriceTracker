using PriceIntelligence.Application.Common;
using PriceIntelligence.Application.DTOs.Auth;
using PriceIntelligence.Application.DTOs.Products;
using PriceIntelligence.Application.DTOs.Watchlists;
using PriceIntelligence.Application.DTOs.Alerts;
using PriceIntelligence.Application.DTOs.Prices;
using PriceIntelligence.Application.DTOs.Admin;

namespace PriceIntelligence.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken);
}

public interface IProductService
{
    Task<PagedResult<ProductDto>> SearchAsync(ProductSearchRequest request);
    Task<ProductDetailDto?> GetByIdAsync(Guid id);
    Task<List<ProductListingDto>> GetListingsAsync(Guid productId);
    Task<PriceComparisonDto?> GetComparisonAsync(Guid productId);
    Task<ProductDto> CreateAsync(CreateProductRequest request);
    Task ProcessScrapeResultAsync(ScrapeResultRequest result);
}

public interface IWatchlistService
{
    Task<List<WatchlistDto>> GetUserWatchlistsAsync(Guid userId);
    Task<WatchlistDetailDto?> GetWatchlistAsync(Guid watchlistId, Guid userId);
    Task<WatchlistDto> CreateAsync(Guid userId, CreateWatchlistRequest request);
    Task<WatchlistDto> UpdateAsync(Guid watchlistId, Guid userId, UpdateWatchlistRequest request);
    Task DeleteAsync(Guid watchlistId, Guid userId);
    Task AddItemAsync(Guid watchlistId, Guid userId, AddWatchlistItemRequest request);
    Task RemoveItemAsync(Guid watchlistId, Guid userId, Guid productId);
}

public interface IAlertService
{
    Task<List<AlertDto>> GetUserAlertsAsync(Guid userId);
    Task<AlertDto> CreateAlertAsync(Guid userId, CreateAlertRequest request);
    Task<AlertDto> ToggleAlertAsync(Guid alertId, Guid userId);
    Task DeleteAlertAsync(Guid alertId, Guid userId);
    Task EvaluateAlertsAsync();
}

public interface IPriceService
{
    Task<List<PriceHistoryDto>> GetPriceHistoryAsync(Guid productId, int days = 30);
    Task<RetailerPriceDto?> GetLowestPriceAsync(Guid productId);
}

public interface IAdminService
{
    Task<SystemStatsDto> GetStatsAsync();
    Task<List<RetailerDto>> GetRetailersAsync();
    Task<RetailerDto> UpdateRetailerAsync(int id, RetailerDto dto);
    Task<List<ScraperJobDto>> GetScraperJobsAsync(int count = 20);
    Task<ScraperJobDto> TriggerScraperAsync(TriggerScraperRequest request);
}

public interface INotificationService
{
    Task SendPriceAlertEmailAsync(Guid userId, string productName, decimal oldPrice, decimal newPrice, string productUrl);
    Task SendStockAlertEmailAsync(Guid userId, string productName, string productUrl);
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task RemoveAsync(string key);
    Task RemoveByPrefixAsync(string prefix);
}

public interface IQueueService
{
    Task EnqueueAsync<T>(string queueName, T message);
    Task<T?> DequeueAsync<T>(string queueName);
}