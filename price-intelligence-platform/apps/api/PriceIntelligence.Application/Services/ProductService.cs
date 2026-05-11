using PriceIntelligence.Application.Common;
using PriceIntelligence.Application.DTOs.Prices;
using PriceIntelligence.Application.DTOs.Products;
using PriceIntelligence.Application.Interfaces;
using PriceIntelligence.Domain.Entities;
using PriceIntelligence.Domain.Enums;

namespace PriceIntelligence.Application.Services;

public class ProductService(
    IProductRepository productRepo,
    IProductListingRepository listingRepo,
    IPriceHistoryRepository priceHistoryRepo,
    ICacheService cache) : IProductService
{
    private const string CachePrefix = "products:";

    public async Task<PagedResult<ProductDto>> SearchAsync(ProductSearchRequest req)
    {
        var cacheKey = $"{CachePrefix}search:{req.Query}:{req.Category}:{req.Brand}:{req.MinPrice}:{req.MaxPrice}:{req.Page}:{req.PageSize}";
        var cached = await cache.GetAsync<PagedResult<ProductDto>>(cacheKey);
        if (cached is not null) return cached;

        var (items, total) = await productRepo.SearchAsync(
            req.Query, req.Category, req.Brand, req.MinPrice, req.MaxPrice, req.Page, req.PageSize);

        var result = new PagedResult<ProductDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Page = req.Page,
            PageSize = req.PageSize,
            TotalCount = total
        };

        await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }

    public async Task<ProductDetailDto?> GetByIdAsync(Guid id)
    {
        var cacheKey = $"{CachePrefix}detail:{id}";
        var cached = await cache.GetAsync<ProductDetailDto>(cacheKey);
        if (cached is not null) return cached;

        var product = await productRepo.GetByIdWithListingsAsync(id);
        if (product is null) return null;

        var dto = MapToDetailDto(product);
        await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10));
        return dto;
    }

    public async Task<List<ProductListingDto>> GetListingsAsync(Guid productId)
    {
        var listings = await listingRepo.GetByProductIdAsync(productId);
        return listings.Select(MapListingToDto).ToList();
    }

    public async Task<PriceComparisonDto?> GetComparisonAsync(Guid productId)
    {
        var product = await productRepo.GetByIdWithListingsAsync(productId);
        if (product is null) return null;

        var retailers = new List<RetailerPriceDto>();
        foreach (var listing in product.Listings.Where(l => l.IsActive))
        {
            var lowest = await priceHistoryRepo.GetLowestEverAsync(listing.Id);
            retailers.Add(new RetailerPriceDto(
                listing.Retailer.Name, listing.Retailer.LogoUrl,
                listing.Price, listing.OriginalPrice, listing.StockStatus.ToString(),
                listing.ProductUrl, lowest?.Price, null));
        }

        return new PriceComparisonDto(productId, product.Name,
            retailers.OrderBy(r => r.CurrentPrice).ToList());
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest req)
    {
        var product = new Product
        {
            Name = req.Name, Brand = req.Brand, Category = req.Category,
            SubCategory = req.SubCategory, Description = req.Description,
            ImageUrl = req.ImageUrl, Barcode = req.Barcode
        };
        await productRepo.CreateAsync(product);
        await cache.RemoveByPrefixAsync(CachePrefix);
        return MapToDto(product);
    }

    public async Task ProcessScrapeResultAsync(ScrapeResultRequest result)
    {
        var existing = await listingRepo.GetByUrlAsync(result.RetailerId, result.ProductUrl);

        if (existing is not null)
        {
            if (existing.Price != result.Price ||
                existing.StockStatus.ToString() != result.StockStatus)
            {
                await priceHistoryRepo.AddAsync(new PriceHistory
                {
                    ProductListingId = existing.Id,
                    Price = result.Price,
                    OriginalPrice = result.OriginalPrice,
                    StockStatus = Enum.Parse<StockStatus>(result.StockStatus, true)
                });
                existing.Price = result.Price;
                existing.OriginalPrice = result.OriginalPrice;
                existing.StockStatus = Enum.Parse<StockStatus>(result.StockStatus, true);
                existing.LastScrapedAt = DateTime.UtcNow;
                await listingRepo.UpdateAsync(existing);
                await cache.RemoveByPrefixAsync(CachePrefix);
            }
        }
        else
        {
            var product = new Product { Name = result.Title, ImageUrl = result.ImageUrl };
            await productRepo.CreateAsync(product);

            var listing = new ProductListing
            {
                ProductId = product.Id, RetailerId = result.RetailerId,
                ExternalId = result.ExternalId, Title = result.Title,
                Price = result.Price, OriginalPrice = result.OriginalPrice,
                StockStatus = Enum.Parse<StockStatus>(result.StockStatus, true),
                ProductUrl = result.ProductUrl, ImageUrl = result.ImageUrl
            };
            await listingRepo.CreateAsync(listing);
            await priceHistoryRepo.AddAsync(new PriceHistory
            {
                ProductListingId = listing.Id,
                Price = listing.Price,
                OriginalPrice = listing.OriginalPrice,
                StockStatus = listing.StockStatus
            });
        }
    }

    private static ProductDto MapToDto(Product p)
    {
        var prices = p.Listings.Where(l => l.IsActive).Select(l => l.Price).ToList();
        return new ProductDto(p.Id, p.Name, p.Brand, p.Category, p.SubCategory,
            p.Description, p.ImageUrl,
            prices.Any() ? prices.Min() : null,
            prices.Any() ? prices.Max() : null,
            p.Listings.Count(l => l.IsActive), p.CreatedAt);
    }

    private static ProductDetailDto MapToDetailDto(Product p) => new(
        p.Id, p.Name, p.Brand, p.Category, p.SubCategory, p.Description, p.ImageUrl,
        p.Listings.Where(l => l.IsActive).Select(l => l.Price).DefaultIfEmpty(0).Min(),
        p.Listings.Where(l => l.IsActive).Select(l => l.Price).DefaultIfEmpty(0).Max(),
        p.Listings.Where(l => l.IsActive).Select(MapListingToDto).ToList(),
        p.CreatedAt);

    private static ProductListingDto MapListingToDto(ProductListing l) => new(
        l.Id, l.ProductId, l.Retailer?.Name ?? "", l.Retailer?.LogoUrl,
        l.Title, l.Price, l.OriginalPrice, l.Currency,
        l.StockStatus.ToString(), l.ProductUrl, l.ImageUrl, l.LastScrapedAt);
}