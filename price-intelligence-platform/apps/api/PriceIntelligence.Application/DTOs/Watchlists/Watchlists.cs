namespace PriceIntelligence.Application.DTOs.Watchlists;

public record WatchlistDto(
    Guid Id, string Name, string? Description,
    bool IsDefault, int ItemCount, DateTime CreatedAt
);

public record WatchlistDetailDto(
    Guid Id, string Name, string? Description,
    bool IsDefault, List<WatchlistItemDto> Items, DateTime CreatedAt
);

public record WatchlistItemDto(
    long Id, Guid ProductId, string ProductName, string? ProductBrand,
    string? ProductImageUrl, decimal? LowestPrice, DateTime AddedAt
);

public record CreateWatchlistRequest(string Name, string? Description);
public record UpdateWatchlistRequest(string Name, string? Description);
public record AddWatchlistItemRequest(Guid ProductId);
