from __future__ import annotations

import asyncio
import random
from abc import ABC, abstractmethod
from typing import Optional

from loguru import logger
from playwright.async_api import (
    async_playwright,
    Browser,
    BrowserContext,
    Page,
    TimeoutError as PWTimeout,
)
from tenacity import (
    retry,
    stop_after_attempt,
    wait_exponential,
    retry_if_exception_type,
)

from models.product import ScrapedProduct


class BaseScraper(ABC):
    RETAILER_ID: int = 0
    RETAILER_NAME: str = "Unknown"
    BASE_URL: str = ""
    RATE_LIMIT: tuple[float, float] = (2.0, 5.0)

    def __init__(self) -> None:
        self._browser: Optional[Browser] = None
        self._context: Optional[BrowserContext] = None

    async def __aenter__(self) -> "BaseScraper":
        self._pw = await async_playwright().start()
        self._browser = await self._pw.chromium.launch(
            headless=True,
            args=[
                "--no-sandbox",
                "--disable-dev-shm-usage",
                "--disable-blink-features=AutomationControlled",
            ],
        )
        assert self._browser is not None
        self._context = await self._browser.new_context(
            viewport={"width": 1366, "height": 768},
            locale="en-ZA",
            extra_http_headers={"Accept-Language": "en-ZA,en;q=0.9"},
        )
        assert self._context is not None
        await self._context.route(
            "**/*.{png,jpg,jpeg,gif,svg,ico,woff,woff2,ttf}",
            lambda route: route.abort(),
        )
        return self

    async def __aexit__(self, *_) -> None:
        if self._context:
            await self._context.close()
        if self._browser:
            await self._browser.close()
        await self._pw.stop()

    async def _new_page(self) -> Page:
        assert self._context
        page = await self._context.new_page()
        # Hide automation signals
        await page.add_init_script(
            "Object.defineProperty(navigator,'webdriver',{get:()=>undefined});"
        )
        return page

    async def _human_delay(self) -> None:
        await asyncio.sleep(random.uniform(*self.RATE_LIMIT))

    @retry(
        stop=stop_after_attempt(3),
        wait=wait_exponential(multiplier=1, min=3, max=20),
        retry=retry_if_exception_type(Exception),
        reraise=True,
    )
    async def _fetch(self, url: str, wait_selector: str = "body") -> Page:
        page = await self._new_page()
        try:
            logger.debug(f"[{self.RETAILER_NAME}] GET {url}")
            await page.goto(url, wait_until="domcontentloaded", timeout=30_000)
            await page.wait_for_selector(wait_selector, timeout=10_000)
            await self._human_delay()
            return page
        except Exception as e:
            await page.close()
            logger.warning(f"[{self.RETAILER_NAME}] Fetch failed: {e}")
            raise

    @abstractmethod
    async def scrape_product(self, url: str) -> Optional[ScrapedProduct]:
        ...

    @abstractmethod
    async def scrape_category(
        self, category_url: str, max_pages: int = 3
    ) -> list[ScrapedProduct]:
        ...

    async def run(self, urls: list[str]) -> list[ScrapedProduct]:
        results: list[ScrapedProduct] = []
        async with self:
            for url in urls:
                try:
                    product = await self.scrape_product(url)
                    if product:
                        results.append(product)
                        logger.info(
                            f"[{self.RETAILER_NAME}] ✓ {product.title} @ R{product.price:.2f}"
                        )
                except Exception as e:
                    logger.error(f"[{self.RETAILER_NAME}] ✗ {url}: {e}")
        return results