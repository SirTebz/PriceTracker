namespace PriceIntelligence.Application.DTOs.Prices;

public record PriceHistoryDto(
    long Id, Guid ProductListingId, string RetailerName,
    decimal Price, decimal? OriginalPrice, string StockStatus, DateTime RecordedAt
);

public record PriceComparisonDto(Guid ProductId, string ProductName, List<RetailerPriceDto> Retailers);

public record RetailerPriceDto(
    string RetailerName, string? RetailerLogoUrl,
    decimal CurrentPrice, decimal? OriginalPrice, string StockStatus,
    string ProductUrl, decimal? LowestEver, decimal? HighestEver
);