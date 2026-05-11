namespace PriceIntelligence.Application.DTOs.Admin;

public record RetailerDto(
    int Id, string Name, string BaseUrl, string Country, string? LogoUrl, bool IsActive
);

public record ScraperJobDto(
    long Id, string RetailerName, string Status, int ItemsScraped,
    string? ErrorMessage, DateTime? StartedAt, DateTime? CompletedAt, DateTime CreatedAt
);

public record TriggerScraperRequest(int? RetailerId = null);

public record SystemStatsDto(
    int TotalProducts, int TotalListings, int TotalUsers,
    int TotalAlerts, int ActiveJobs, DateTime LastScraperRun
);