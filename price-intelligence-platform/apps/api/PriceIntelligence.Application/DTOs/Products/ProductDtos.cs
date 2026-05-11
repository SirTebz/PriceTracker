namespace PriceIntelligence.Application.DTOs.Products;

public record ProductDto(
    Guid Id, string Name, string? Brand, string? Category,
    string? SubCategory, string? Description, string? ImageUrl,
    decimal? LowestPrice, decimal? HighestPrice, int ListingCount, DateTime CreatedAt
);

public record ProductDetailDto(
    Guid Id, string Name, string? Brand, string? Category,
    string? SubCategory, string? Description, string? ImageUrl,
    decimal? LowestPrice, decimal? HighestPrice,
    List<ProductListingDto> Listings, DateTime CreatedAt
);

public record ProductListingDto(
    Guid Id, Guid ProductId, string RetailerName, string? RetailerLogoUrl,
    string Title, decimal Price, decimal? OriginalPrice, string Currency,
    string StockStatus, string ProductUrl, string? ImageUrl, DateTime LastScrapedAt
);

public record ProductSearchRequest(
    string? Query = null, string? Category = null, string? Brand = null,
    decimal? MinPrice = null, decimal? MaxPrice = null,
    int Page = 1, int PageSize = 20
);

public record CreateProductRequest(
    string Name, string? Brand, string? Category, string? SubCategory,
    string? Description, string? ImageUrl, string? Barcode
);

public record ScrapeResultRequest(
    int RetailerId, string Title, decimal Price, decimal? OriginalPrice,
    string StockStatus, string ProductUrl, string? ImageUrl, string? ExternalId
);