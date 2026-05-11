namespace PriceIntelligence.Domain.Enums;

public enum UserRole { User, Admin }

public enum StockStatus { Unknown, InStock, OutOfStock, LowStock }

public enum AlertConditionType
{
    PriceBelow,
    PriceDrop,
    BackInStock,
    AnyChange
}