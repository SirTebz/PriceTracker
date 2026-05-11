using PriceIntelligence.Application.Common;
using PriceIntelligence.Application.DTOs.Admin;
using PriceIntelligence.Application.DTOs.Alerts;
using PriceIntelligence.Application.DTOs.Prices;
using PriceIntelligence.Application.DTOs.Watchlists;
using PriceIntelligence.Application.Interfaces;
using PriceIntelligence.Domain.Entities;
using PriceIntelligence.Domain.Enums;

namespace PriceIntelligence.Application.Services;

// ─── WatchlistService ─────────────────────────────────────────────────────────
public class WatchlistService(IWatchlistRepository repo) : IWatchlistService
{
    public async Task<List<WatchlistDto>> GetUserWatchlistsAsync(Guid userId)
    {
        var lists = await repo.GetByUserIdAsync(userId);
        return lists.Select(w => new WatchlistDto(
            w.Id, w.Name, w.Description, w.IsDefault, w.Items.Count, w.CreatedAt)).ToList();
    }

    public async Task<WatchlistDetailDto?> GetWatchlistAsync(Guid watchlistId, Guid userId)
    {
        var w = await repo.GetByIdAsync(watchlistId, userId);
        if (w is null) return null;
        var items = w.Items.Select(i => new WatchlistItemDto(
            i.Id, i.ProductId, i.Product?.Name ?? "", i.Product?.Brand,
            i.Product?.ImageUrl,
            i.Product?.Listings.Where(l => l.IsActive).Select(l => l.Price).DefaultIfEmpty(0).Min(),
            i.AddedAt)).ToList();
        return new WatchlistDetailDto(w.Id, w.Name, w.Description, w.IsDefault, items, w.CreatedAt);
    }

    public async Task<WatchlistDto> CreateAsync(Guid userId, CreateWatchlistRequest req)
    {
        var w = new Watchlist { UserId = userId, Name = req.Name, Description = req.Description };
        await repo.CreateAsync(w);
        return new WatchlistDto(w.Id, w.Name, w.Description, w.IsDefault, 0, w.CreatedAt);
    }

    public async Task<WatchlistDto> UpdateAsync(Guid watchlistId, Guid userId, UpdateWatchlistRequest req)
    {
        var w = await repo.GetByIdAsync(watchlistId, userId)
            ?? throw new ServiceException("Watchlist not found.", 404);
        w.Name = req.Name;
        w.Description = req.Description;
        w.UpdatedAt = DateTime.UtcNow;
        await repo.UpdateAsync(w);
        return new WatchlistDto(w.Id, w.Name, w.Description, w.IsDefault, w.Items.Count, w.CreatedAt);
    }

    public async Task DeleteAsync(Guid watchlistId, Guid userId)
    {
        var w = await repo.GetByIdAsync(watchlistId, userId)
            ?? throw new ServiceException("Watchlist not found.", 404);
        await repo.DeleteAsync(w.Id);
    }

    public async Task AddItemAsync(Guid watchlistId, Guid userId, AddWatchlistItemRequest req)
    {
        _ = await repo.GetByIdAsync(watchlistId, userId)
            ?? throw new ServiceException("Watchlist not found.", 404);
        if (await repo.ItemExistsAsync(watchlistId, req.ProductId))
            throw new ServiceException("Product already in watchlist.", 409);
        await repo.AddItemAsync(new WatchlistItem { WatchlistId = watchlistId, ProductId = req.ProductId });
    }

    public async Task RemoveItemAsync(Guid watchlistId, Guid userId, Guid productId)
    {
        _ = await repo.GetByIdAsync(watchlistId, userId)
            ?? throw new ServiceException("Watchlist not found.", 404);
        await repo.RemoveItemAsync(watchlistId, productId);
    }
}

// ─── AlertService ─────────────────────────────────────────────────────────────
public class AlertService(
    IAlertRepository alertRepo,
    IProductRepository productRepo,
    IPriceHistoryRepository priceRepo,
    INotificationService notifyService) : IAlertService
{
    public async Task<List<AlertDto>> GetUserAlertsAsync(Guid userId)
    {
        var alerts = await alertRepo.GetByUserIdAsync(userId);
        return alerts.Select(a => new AlertDto(
            a.Id, a.ProductId, a.Product?.Name ?? "",
            a.ConditionType.ToString(), a.ThresholdValue,
            a.IsActive, a.LastTriggeredAt, a.CreatedAt)).ToList();
    }

    public async Task<AlertDto> CreateAlertAsync(Guid userId, CreateAlertRequest req)
    {
        var product = await productRepo.GetByIdAsync(req.ProductId)
            ?? throw new ServiceException("Product not found.", 404);
        if (!Enum.TryParse<AlertConditionType>(req.ConditionType, true, out var condition))
            throw new ServiceException($"Invalid condition type: {req.ConditionType}");

        var alert = new Alert
        {
            UserId = userId, ProductId = req.ProductId,
            ConditionType = condition, ThresholdValue = req.ThresholdValue
        };
        await alertRepo.CreateAsync(alert);
        return new AlertDto(alert.Id, alert.ProductId, product.Name,
            alert.ConditionType.ToString(), alert.ThresholdValue, true, null, alert.CreatedAt);
    }

    public async Task<AlertDto> ToggleAlertAsync(Guid alertId, Guid userId)
    {
        var alert = await alertRepo.GetByIdAsync(alertId)
            ?? throw new ServiceException("Alert not found.", 404);
        if (alert.UserId != userId) throw new ServiceException("Forbidden.", 403);
        alert.IsActive = !alert.IsActive;
        await alertRepo.UpdateAsync(alert);
        return new AlertDto(alert.Id, alert.ProductId, alert.Product?.Name ?? "",
            alert.ConditionType.ToString(), alert.ThresholdValue,
            alert.IsActive, alert.LastTriggeredAt, alert.CreatedAt);
    }

    public async Task DeleteAlertAsync(Guid alertId, Guid userId)
    {
        var alert = await alertRepo.GetByIdAsync(alertId)
            ?? throw new ServiceException("Alert not found.", 404);
        if (alert.UserId != userId) throw new ServiceException("Forbidden.", 403);
        await alertRepo.DeleteAsync(alertId);
    }

    public async Task EvaluateAlertsAsync()
    {
        var alerts = await alertRepo.GetActiveAlertsAsync();
        foreach (var alert in alerts)
        {
            try { await EvaluateSingleAsync(alert); }
            catch { /* log and continue */ }
        }
    }

    private async Task EvaluateSingleAsync(Alert alert)
    {
        var product = await productRepo.GetByIdWithListingsAsync(alert.ProductId);
        if (product is null) return;
        var listings = product.Listings.Where(l => l.IsActive).ToList();
        if (!listings.Any()) return;
        var currentLowest = listings.Min(l => l.Price);

        if (alert.ConditionType == AlertConditionType.PriceBelow && alert.ThresholdValue.HasValue
            && currentLowest < alert.ThresholdValue.Value)
        {
            var listing = listings.OrderBy(l => l.Price).First();
            await notifyService.SendPriceAlertEmailAsync(
                alert.UserId, product.Name, alert.ThresholdValue.Value, currentLowest, listing.ProductUrl);
            alert.LastTriggeredAt = DateTime.UtcNow;
            await alertRepo.UpdateAsync(alert);
        }
        else if (alert.ConditionType == AlertConditionType.BackInStock
            && listings.Any(l => l.StockStatus == StockStatus.InStock))
        {
            var listing = listings.First(l => l.StockStatus == StockStatus.InStock);
            await notifyService.SendStockAlertEmailAsync(alert.UserId, product.Name, listing.ProductUrl);
            alert.LastTriggeredAt = DateTime.UtcNow;
            await alertRepo.UpdateAsync(alert);
        }
    }
}

// ─── PriceService ─────────────────────────────────────────────────────────────
public class PriceService(IPriceHistoryRepository repo, IProductRepository productRepo) : IPriceService
{
    public async Task<List<PriceHistoryDto>> GetPriceHistoryAsync(Guid productId, int days = 30)
    {
        var history = await repo.GetByProductIdAsync(productId, days);
        return history.Select(h => new PriceHistoryDto(
            h.Id, h.ProductListingId, h.ProductListing?.Retailer?.Name ?? "",
            h.Price, h.OriginalPrice, h.StockStatus.ToString(), h.RecordedAt)).ToList();
    }

    public async Task<RetailerPriceDto?> GetLowestPriceAsync(Guid productId)
    {
        var product = await productRepo.GetByIdWithListingsAsync(productId);
        if (product is null) return null;
        var cheapest = product.Listings.Where(l => l.IsActive).OrderBy(l => l.Price).FirstOrDefault();
        if (cheapest is null) return null;
        var lowest = await repo.GetLowestEverAsync(cheapest.Id);
        return new RetailerPriceDto(
            cheapest.Retailer.Name, cheapest.Retailer.LogoUrl,
            cheapest.Price, cheapest.OriginalPrice, cheapest.StockStatus.ToString(),
            cheapest.ProductUrl, lowest?.Price, null);
    }
}

// ─── AdminService ─────────────────────────────────────────────────────────────
public class AdminService(
    IRetailerRepository retailerRepo,
    IScraperJobRepository scraperJobRepo,
    IQueueService queue) : IAdminService
{
    public async Task<SystemStatsDto> GetStatsAsync()
    {
        var jobs = await scraperJobRepo.GetRecentAsync(1);
        return new SystemStatsDto(0, 0, 0, 0,
            jobs.Count(j => j.Status == "Running"),
            jobs.FirstOrDefault()?.CompletedAt ?? DateTime.MinValue);
    }

    public async Task<List<RetailerDto>> GetRetailersAsync()
    {
        var retailers = await retailerRepo.GetAllAsync();
        return retailers.Select(r => new RetailerDto(
            r.Id, r.Name, r.BaseUrl, r.Country, r.LogoUrl, r.IsActive)).ToList();
    }

    public async Task<RetailerDto> UpdateRetailerAsync(int id, RetailerDto dto)
    {
        var retailer = await retailerRepo.GetByIdAsync(id)
            ?? throw new ServiceException("Retailer not found.", 404);
        retailer.Name = dto.Name;
        retailer.BaseUrl = dto.BaseUrl;
        retailer.IsActive = dto.IsActive;
        await retailerRepo.UpdateAsync(retailer);
        return new RetailerDto(retailer.Id, retailer.Name, retailer.BaseUrl,
            retailer.Country, retailer.LogoUrl, retailer.IsActive);
    }

    public async Task<List<ScraperJobDto>> GetScraperJobsAsync(int count = 20)
    {
        var jobs = await scraperJobRepo.GetRecentAsync(count);
        return jobs.Select(j => new ScraperJobDto(
            j.Id, j.Retailer?.Name ?? "", j.Status, j.ItemsScraped,
            j.ErrorMessage, j.StartedAt, j.CompletedAt, j.CreatedAt)).ToList();
    }

    public async Task<ScraperJobDto> TriggerScraperAsync(TriggerScraperRequest req)
    {
        var retailerId = req.RetailerId ?? 1;
        var retailer = await retailerRepo.GetByIdAsync(retailerId)
            ?? throw new ServiceException("Retailer not found.", 404);
        var job = new ScraperJob { RetailerId = retailerId, Status = "Pending" };
        await scraperJobRepo.CreateAsync(job);
        await queue.EnqueueAsync("scrape-jobs",
            new { JobId = job.Id, RetailerId = retailerId, RetailerName = retailer.Name });
        return new ScraperJobDto(job.Id, retailer.Name, job.Status, 0, null, null, null, job.CreatedAt);
    }
}