using System.Text.Json;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using StackExchange.Redis;
using PriceIntelligence.Application.Interfaces;

namespace PriceIntelligence.Infrastructure.Services;

public class RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger) : ICacheService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key)
    {
        try {
            var value = await _db.StringGetAsync(key);
            return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : default;
        }
        catch (Exception ex) { logger.LogWarning(ex, "Cache get failed: {Key}", key); return default; }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try {
            await _db.StringSetAsync(key, JsonSerializer.Serialize(value), expiry ?? TimeSpan.FromMinutes(15));
        }
        catch (Exception ex) { logger.LogWarning(ex, "Cache set failed: {Key}", key); }
    }

    public async Task RemoveAsync(string key) => await _db.KeyDeleteAsync(key);

    public async Task RemoveByPrefixAsync(string prefix)
    {
        try {
            var server = redis.GetServer(redis.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{prefix}*").ToArray();
            if (keys.Length > 0) await _db.KeyDeleteAsync(keys);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Cache prefix delete failed: {Prefix}", prefix); }
    }
}

public class RedisQueueService(IConnectionMultiplexer redis, ILogger<RedisQueueService> logger) : IQueueService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task EnqueueAsync<T>(string queueName, T message)
    {
        try { await _db.ListRightPushAsync(queueName, JsonSerializer.Serialize(message)); }
        catch (Exception ex) { logger.LogError(ex, "Enqueue failed: {Queue}", queueName); throw; }
    }

    public async Task<T?> DequeueAsync<T>(string queueName)
    {
        try {
            var value = await _db.ListLeftPopAsync(queueName);
            return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : default;
        }
        catch (Exception ex) { logger.LogWarning(ex, "Dequeue failed: {Queue}", queueName); return default; }
    }
}

public class EmailNotificationService(IConfiguration config, ILogger<EmailNotificationService> logger) : INotificationService
{
    public Task SendPriceAlertEmailAsync(Guid userId, string productName, decimal oldPrice, decimal newPrice, string productUrl) =>
        SendAsync(
            $"💰 Price Drop: {productName}",
            $"<h2>Price Drop Alert!</h2><p><strong>{productName}</strong> dropped from <s>R{oldPrice:N2}</s> to <strong style='color:green'>R{newPrice:N2}</strong></p><p><a href='{productUrl}'>View Product →</a></p>"
        );

    public Task SendStockAlertEmailAsync(Guid userId, string productName, string productUrl) =>
        SendAsync(
            $"📦 Back In Stock: {productName}",
            $"<h2>Back In Stock!</h2><p><strong>{productName}</strong> is available again.</p><p><a href='{productUrl}'>Buy Now →</a></p>"
        );

    private async Task SendAsync(string subject, string html)
    {
        var user = config["Email:SmtpUser"] ?? "";
        if (string.IsNullOrEmpty(user)) {
            logger.LogWarning("SMTP not configured — skipping email: {Subject}", subject);
            return;
        }
        try {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(config["Email:FromAddress"] ?? "noreply@priceiq.local"));
            message.To.Add(MailboxAddress.Parse(config["Email:FromAddress"] ?? "noreply@priceiq.local"));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = html };

            using var client = new SmtpClient();
            await client.ConnectAsync(config["Email:SmtpHost"], int.Parse(config["Email:SmtpPort"] ?? "587"),
                MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(user, config["Email:SmtpPass"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex) { logger.LogError(ex, "Email send failed: {Subject}", subject); }
    }
}