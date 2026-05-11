using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using PriceIntelligence.Application.Interfaces;
using PriceIntelligence.Infrastructure.Data;
using PriceIntelligence.Infrastructure.Repositories;
using PriceIntelligence.Infrastructure.Services;

namespace PriceIntelligence.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseSqlServer(config.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));

        var redisConn = config["Redis:ConnectionString"] ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConn));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductListingRepository, ProductListingRepository>();
        services.AddScoped<IPriceHistoryRepository, PriceHistoryRepository>();
        services.AddScoped<IWatchlistRepository, WatchlistRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IRetailerRepository, RetailerRepository>();
        services.AddScoped<IScraperJobRepository, ScraperJobRepository>();

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<IQueueService, RedisQueueService>();
        services.AddScoped<INotificationService, EmailNotificationService>();

        return services;
    }
}