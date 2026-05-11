using Microsoft.EntityFrameworkCore;
using PriceIntelligence.Application.Interfaces;
using PriceIntelligence.Domain.Entities;
using PriceIntelligence.Infrastructure.Data;

namespace PriceIntelligence.Infrastructure.Repositories;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id) => db.Users.FindAsync(id).AsTask();
    public Task<User?> GetByEmailAsync(string email) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email);
    public async Task<User> CreateAsync(User user) {
        db.Users.Add(user); await db.SaveChangesAsync(); return user; }
    public Task UpdateAsync(User user) {
        db.Users.Update(user); return db.SaveChangesAsync(); }
}

public class ProductRepository(AppDbContext db) : IProductRepository
{
    public async Task<(List<Product>, int)> SearchAsync(
        string? query, string? category, string? brand,
        decimal? minPrice, decimal? maxPrice, int page, int pageSize)
    {
        var q = db.Products
            .Include(p => p.Listings.Where(l => l.IsActive))
                .ThenInclude(l => l.Retailer)
            .Where(p => p.IsActive).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(p => p.Name.Contains(query) ||
                (p.Brand != null && p.Brand.Contains(query)));
        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(p => p.Category == category);
        if (!string.IsNullOrWhiteSpace(brand))
            q = q.Where(p => p.Brand == brand);
        if (minPrice.HasValue)
            q = q.Where(p => p.Listings.Any(l => l.Price >= minPrice.Value));
        if (maxPrice.HasValue)
            q = q.Where(p => p.Listings.Any(l => l.Price <= maxPrice.Value));

        var total = await q.CountAsync();
        var items = await q.OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public Task<Product?> GetByIdAsync(Guid id) => db.Products.FindAsync(id).AsTask();

    public Task<Product?> GetByIdWithListingsAsync(Guid id) => db.Products
        .Include(p => p.Listings.Where(l => l.IsActive))
            .ThenInclude(l => l.Retailer)
        .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Product> CreateAsync(Product product) {
        db.Products.Add(product); await db.SaveChangesAsync(); return product; }
    public Task UpdateAsync(Product product) {
        db.Products.Update(product); return db.SaveChangesAsync(); }
}

public class ProductListingRepository(AppDbContext db) : IProductListingRepository
{
    public Task<List<ProductListing>> GetByProductIdAsync(Guid productId) =>
        db.ProductListings.Include(l => l.Retailer)
            .Where(l => l.ProductId == productId && l.IsActive).ToListAsync();

    public Task<ProductListing?> GetByUrlAsync(int retailerId, string url) =>
        db.ProductListings.FirstOrDefaultAsync(
            l => l.RetailerId == retailerId && l.ProductUrl == url);

    public async Task<ProductListing> CreateAsync(ProductListing listing) {
        db.ProductListings.Add(listing); await db.SaveChangesAsync(); return listing; }
    public Task UpdateAsync(ProductListing listing) {
        db.ProductListings.Update(listing); return db.SaveChangesAsync(); }
}

public class PriceHistoryRepository(AppDbContext db) : IPriceHistoryRepository
{
    public Task AddAsync(PriceHistory h) {
        db.PriceHistories.Add(h); return db.SaveChangesAsync(); }

    public async Task<List<PriceHistory>> GetByProductIdAsync(Guid productId, int days)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        return await db.PriceHistories
            .Include(h => h.ProductListing).ThenInclude(l => l.Retailer)
            .Where(h => h.ProductListing.ProductId == productId && h.RecordedAt >= since)
            .OrderByDescending(h => h.RecordedAt).ToListAsync();
    }

    public Task<PriceHistory?> GetLowestEverAsync(Guid listingId) =>
        db.PriceHistories.Where(h => h.ProductListingId == listingId)
            .OrderBy(h => h.Price).FirstOrDefaultAsync();
}

public class WatchlistRepository(AppDbContext db) : IWatchlistRepository
{
    public Task<List<Watchlist>> GetByUserIdAsync(Guid userId) =>
        db.Watchlists.Include(w => w.Items)
            .Where(w => w.UserId == userId).ToListAsync();

    public Task<Watchlist?> GetByIdAsync(Guid id, Guid userId) =>
        db.Watchlists
            .Include(w => w.Items).ThenInclude(i => i.Product)
                .ThenInclude(p => p!.Listings.Where(l => l.IsActive))
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);

    public async Task<Watchlist> CreateAsync(Watchlist w) {
        db.Watchlists.Add(w); await db.SaveChangesAsync(); return w; }
    public Task UpdateAsync(Watchlist w) {
        db.Watchlists.Update(w); return db.SaveChangesAsync(); }
    public async Task DeleteAsync(Guid id)
    {
        var w = await db.Watchlists.FindAsync(id);
        if (w != null) { db.Watchlists.Remove(w); await db.SaveChangesAsync(); }
    }
    public async Task AddItemAsync(WatchlistItem item) {
        db.WatchlistItems.Add(item); await db.SaveChangesAsync(); }
    public async Task RemoveItemAsync(Guid watchlistId, Guid productId)
    {
        var item = await db.WatchlistItems.FirstOrDefaultAsync(
            i => i.WatchlistId == watchlistId && i.ProductId == productId);
        if (item != null) { db.WatchlistItems.Remove(item); await db.SaveChangesAsync(); }
    }
    public Task<bool> ItemExistsAsync(Guid watchlistId, Guid productId) =>
        db.WatchlistItems.AnyAsync(i => i.WatchlistId == watchlistId && i.ProductId == productId);
}

public class AlertRepository(AppDbContext db) : IAlertRepository
{
    public Task<List<Alert>> GetByUserIdAsync(Guid userId) =>
        db.Alerts.Include(a => a.Product).Where(a => a.UserId == userId).ToListAsync();
    public Task<Alert?> GetByIdAsync(Guid id) =>
        db.Alerts.Include(a => a.Product).FirstOrDefaultAsync(a => a.Id == id);
    public Task<List<Alert>> GetActiveAlertsAsync() =>
        db.Alerts.Include(a => a.Product).Where(a => a.IsActive).ToListAsync();
    public async Task<Alert> CreateAsync(Alert alert) {
        db.Alerts.Add(alert); await db.SaveChangesAsync(); return alert; }
    public Task UpdateAsync(Alert alert) {
        db.Alerts.Update(alert); return db.SaveChangesAsync(); }
    public async Task DeleteAsync(Guid id)
    {
        var a = await db.Alerts.FindAsync(id);
        if (a != null) { db.Alerts.Remove(a); await db.SaveChangesAsync(); }
    }
}

public class RetailerRepository(AppDbContext db) : IRetailerRepository
{
    public Task<List<Retailer>> GetAllAsync() => db.Retailers.ToListAsync();
    public Task<Retailer?> GetByIdAsync(int id) => db.Retailers.FindAsync(id).AsTask();
    public Task UpdateAsync(Retailer r) {
        db.Retailers.Update(r); return db.SaveChangesAsync(); }
}

public class ScraperJobRepository(AppDbContext db) : IScraperJobRepository
{
    public Task<List<ScraperJob>> GetRecentAsync(int count) =>
        db.ScraperJobs.Include(j => j.Retailer)
            .OrderByDescending(j => j.CreatedAt).Take(count).ToListAsync();
    public async Task<ScraperJob> CreateAsync(ScraperJob job) {
        db.ScraperJobs.Add(job); await db.SaveChangesAsync(); return job; }
    public Task UpdateAsync(ScraperJob job) {
        db.ScraperJobs.Update(job); return db.SaveChangesAsync(); }
}