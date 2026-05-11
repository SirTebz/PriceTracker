using System.Net;
using System.Text.Json;
using PriceIntelligence.Application.Common;
using PriceIntelligence.Application.Interfaces;

namespace PriceIntelligence.API.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (ServiceException ex)
        {
            ctx.Response.StatusCode = ex.StatusCode;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(
                JsonSerializer.Serialize(ApiResponse<object>.Fail(ex.Message)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(
                JsonSerializer.Serialize(ApiResponse<object>.Fail("An unexpected error occurred.")));
        }
    }
}

public class AlertEvaluationWorker(IServiceScopeFactory scopeFactory, ILogger<AlertEvaluationWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Alert worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try {
                using var scope = scopeFactory.CreateScope();
                var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();
                await alertService.EvaluateAlertsAsync();
            }
            catch (Exception ex) { logger.LogError(ex, "Alert evaluation error."); }
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}