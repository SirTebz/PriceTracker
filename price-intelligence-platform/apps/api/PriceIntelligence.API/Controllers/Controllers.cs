using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceIntelligence.Application.Common;
using PriceIntelligence.Application.DTOs.Admin;
using PriceIntelligence.Application.DTOs.Alerts;
using PriceIntelligence.Application.DTOs.Auth;
using PriceIntelligence.Application.DTOs.Products;
using PriceIntelligence.Application.DTOs.Watchlists;
using PriceIntelligence.Application.Interfaces;

namespace PriceIntelligence.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseController : ControllerBase
{
    protected Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException());

    protected new IActionResult Ok<T>(T data, string? msg = null) =>
        base.Ok(ApiResponse<T>.Ok(data, msg));

    protected IActionResult Created<T>(T data, string? msg = null) =>
        StatusCode(201, ApiResponse<T>.Ok(data, msg));
}

// ─── Auth ─────────────────────────────────────────────────────────────────────
[Route("api/auth")]
public class AuthController(IAuthService authService) : BaseController
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req) =>
        Created(await authService.RegisterAsync(req), "Registration successful.");

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req) =>
        Ok(await authService.LoginAsync(req));

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new { UserId = CurrentUserId });
}

// ─── Products ─────────────────────────────────────────────────────────────────
[Route("api/products")]
public class ProductsController(IProductService productService) : BaseController
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] ProductSearchRequest req) =>
        Ok(await productService.SearchAsync(req));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var p = await productService.GetByIdAsync(id);
        return p is null ? NotFound(ApiResponse<object>.Fail("Product not found.")) : Ok(p);
    }

    [HttpGet("{id:guid}/listings")]
    public async Task<IActionResult> GetListings(Guid id) =>
        Ok(await productService.GetListingsAsync(id));

    [HttpGet("{id:guid}/compare")]
    public async Task<IActionResult> Compare(Guid id)
    {
        var r = await productService.GetComparisonAsync(id);
        return r is null ? NotFound(ApiResponse<object>.Fail("Product not found.")) : Ok(r);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest req) =>
        Created(await productService.CreateAsync(req));
}

// ─── Watchlists ───────────────────────────────────────────────────────────────
[Authorize]
[Route("api/watchlists")]
public class WatchlistsController(IWatchlistService watchlistService) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await watchlistService.GetUserWatchlistsAsync(CurrentUserId));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var r = await watchlistService.GetWatchlistAsync(id, CurrentUserId);
        return r is null ? NotFound(ApiResponse<object>.Fail("Not found.")) : Ok(r);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWatchlistRequest req) =>
        Created(await watchlistService.CreateAsync(CurrentUserId, req));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWatchlistRequest req) =>
        Ok(await watchlistService.UpdateAsync(id, CurrentUserId, req));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await watchlistService.DeleteAsync(id, CurrentUserId);
        return Ok<object>(null!, "Deleted.");
    }

    [HttpPost("{id:guid}/items")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddWatchlistItemRequest req)
    {
        await watchlistService.AddItemAsync(id, CurrentUserId, req);
        return Ok<object>(null!, "Item added.");
    }

    [HttpDelete("{id:guid}/items/{productId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid id, Guid productId)
    {
        await watchlistService.RemoveItemAsync(id, CurrentUserId, productId);
        return Ok<object>(null!, "Item removed.");
    }
}

// ─── Alerts ───────────────────────────────────────────────────────────────────
[Authorize]
[Route("api/alerts")]
public class AlertsController(IAlertService alertService) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await alertService.GetUserAlertsAsync(CurrentUserId));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAlertRequest req) =>
        Created(await alertService.CreateAlertAsync(CurrentUserId, req));

    [HttpPatch("{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid id) =>
        Ok(await alertService.ToggleAlertAsync(id, CurrentUserId));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await alertService.DeleteAlertAsync(id, CurrentUserId);
        return Ok<object>(null!, "Deleted.");
    }
}

// ─── Prices ───────────────────────────────────────────────────────────────────
[Route("api/prices")]
public class PricesController(IPriceService priceService) : BaseController
{
    [HttpGet("{productId:guid}")]
    public async Task<IActionResult> GetHistory(Guid productId, [FromQuery] int days = 30) =>
        Ok(await priceService.GetPriceHistoryAsync(productId, days));

    [HttpGet("lowest/{productId:guid}")]
    public async Task<IActionResult> GetLowest(Guid productId)
    {
        var r = await priceService.GetLowestPriceAsync(productId);
        return r is null ? NotFound(ApiResponse<object>.Fail("No listings found.")) : Ok(r);
    }
}

// ─── Admin ────────────────────────────────────────────────────────────────────
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController(IAdminService adminService) : BaseController
{
    [HttpGet("stats")]
    public async Task<IActionResult> Stats() => Ok(await adminService.GetStatsAsync());

    [HttpGet("retailers")]
    public async Task<IActionResult> GetRetailers() => Ok(await adminService.GetRetailersAsync());

    [HttpPut("retailers/{id:int}")]
    public async Task<IActionResult> UpdateRetailer(int id, [FromBody] RetailerDto dto) =>
        Ok(await adminService.UpdateRetailerAsync(id, dto));

    [HttpGet("scraper-jobs")]
    public async Task<IActionResult> GetJobs([FromQuery] int count = 20) =>
        Ok(await adminService.GetScraperJobsAsync(count));

    [HttpPost("trigger-scraper")]
    public async Task<IActionResult> TriggerScraper([FromBody] TriggerScraperRequest req) =>
        Created(await adminService.TriggerScraperAsync(req));
}

// ─── System (Internal) ────────────────────────────────────────────────────────
[Route("api/system")]
public class SystemController(IProductService productService, IConfiguration config) : BaseController
{
    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] ScrapeResultRequest req)
    {
        if (!Request.Headers.TryGetValue("X-Internal-Key", out var key) ||
            key != config["Services:InternalKey"])
            return Unauthorized(ApiResponse<object>.Fail("Invalid internal key."));

        await productService.ProcessScrapeResultAsync(req);
        return Ok<object>(null!, "Ingested.");
    }
}