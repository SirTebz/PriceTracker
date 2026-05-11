namespace PriceIntelligence.Application.DTOs.Alerts;

public record AlertDto(
    Guid Id, Guid ProductId, string ProductName,
    string ConditionType, decimal? ThresholdValue,
    bool IsActive, DateTime? LastTriggeredAt, DateTime CreatedAt
);

public record CreateAlertRequest(Guid ProductId, string ConditionType, decimal? ThresholdValue);