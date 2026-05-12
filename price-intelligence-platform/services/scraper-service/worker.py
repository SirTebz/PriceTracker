"""
Scraper worker — reads jobs from Redis queue, runs the right scraper,
and posts results to the API ingest endpoint.
"""
import asyncio
import json
import os

import redis.asyncio as aioredis
from dotenv import load_dotenv
from loguru import logger

from models.product import ScrapedProduct
from scrapers.takealot import TakealotScraper
from scrapers.wootware import WootwareScraper
from utils.api_client import ApiClient
from utils.logger import setup_logger

load_dotenv()

REDIS_URL       = os.getenv("REDIS_URL", "redis://localhost:6379")
API_BASE_URL    = os.getenv("API_BASE_URL", "http://localhost:5000")
API_INTERNAL_KEY = os.getenv("API_INTERNAL_KEY", "internal-secret-key-2024")
LOG_LEVEL       = os.getenv("LOG_LEVEL", "INFO")
SCRAPE_INTERVAL = int(os.getenv("SCRAPE_INTERVAL_SECONDS", "3600"))

QUEUE_NAME = "scrape-jobs"

# Map retailer IDs → scrapers
SCRAPER_MAP = {
    1: TakealotScraper,
    3: WootwareScraper,
}

# Default category URLs to scrape on schedule
DEFAULT_CATEGORIES = {
    1: [
        "https://www.takealot.com/electronics/cat",
        "https://www.takealot.com/computers/cat",
    ],
    3: [
        "https://www.wootware.co.za/computer-hardware",
        "https://www.wootware.co.za/storage",
    ],
}


async def process_job(job: dict, api_client: ApiClient) -> None:
    retailer_id = job.get("RetailerId", 1)
    scraper_class = SCRAPER_MAP.get(retailer_id)

    if not scraper_class:
        logger.warning(f"No scraper for retailer ID {retailer_id}")
        return

    scraper = scraper_class()
    categories = DEFAULT_CATEGORIES.get(retailer_id, [])

    if not categories:
        logger.warning(f"No categories configured for retailer {retailer_id}")
        return

    all_products: list[ScrapedProduct] = []
    async with scraper:
        for cat_url in categories:
            try:
                logger.info(f"Scraping category: {cat_url}")
                products = await scraper.scrape_category(cat_url, max_pages=2)
                all_products.extend(products)
                logger.info(f"Got {len(products)} products from {cat_url}")
            except Exception as e:
                logger.error(f"Category scrape failed ({cat_url}): {e}")

    if all_products:
        result = await api_client.ingest_batch(all_products)
        logger.info(
            f"Retailer {retailer_id} done — "
            f"✓ {result['success']} ingested, ✗ {result['failed']} failed"
        )


async def listen_for_jobs(redis_client: aioredis.Redis, api_client: ApiClient) -> None:
    logger.info(f"Listening on queue: {QUEUE_NAME}")
    while True:
        try:
            # Blocking pop — waits up to 5 seconds for a job
            item = await redis_client.blpop(QUEUE_NAME, timeout=5)
            if item:
                _, raw = item
                job = json.loads(raw)
                logger.info(f"Job received: {job}")
                await process_job(job, api_client)
        except Exception as e:
            logger.error(f"Queue error: {e}")
            await asyncio.sleep(5)


async def scheduled_scrape(api_client: ApiClient) -> None:
    """Run all scrapers on a schedule even without a queue job."""
    while True:
        logger.info("Running scheduled scrape for all retailers...")
        for retailer_id in SCRAPER_MAP:
            await process_job({"RetailerId": retailer_id}, api_client)
        logger.info(f"Scheduled scrape complete. Next run in {SCRAPE_INTERVAL}s")
        await asyncio.sleep(SCRAPE_INTERVAL)


async def main() -> None:
    setup_logger(LOG_LEVEL)
    logger.info("PriceIQ Scraper Worker starting...")

    api_client = ApiClient(API_BASE_URL, API_INTERNAL_KEY)

    redis_client = await aioredis.from_url(
        REDIS_URL, encoding="utf-8", decode_responses=True
    )

    # Run queue listener and scheduler concurrently
    await asyncio.gather(
        listen_for_jobs(redis_client, api_client),
        scheduled_scrape(api_client),
    )


if __name__ == "__main__":
    asyncio.run(main())