from __future__ import annotations

import json
import re
from typing import Optional

from loguru import logger
from playwright.async_api import Page

from models.product import ScrapedProduct
from scrapers.base import BaseScraper


class TakealotScraper(BaseScraper):
    RETAILER_ID = 1
    RETAILER_NAME = "Takealot"
    BASE_URL = "https://www.takealot.com"

    async def scrape_product(self, url: str) -> Optional[ScrapedProduct]:
        page = await self._fetch(url, wait_selector="[class*='product-title']")
        try:
            # Title
            title_el = await page.query_selector("[class*='product-title']")
            title = (await title_el.inner_text()).strip() if title_el else ""

            # Price — Takealot uses a buybox price element
            price = 0.0
            price_el = await page.query_selector("[class*='buybox-price']")
            if not price_el:
                price_el = await page.query_selector("[class*='price']")
            if price_el:
                raw = await price_el.inner_text()
                price = self._parse_price(raw)

            # Original price (if on sale)
            original_price: Optional[float] = None
            orig_el = await page.query_selector("[class*='original-price']")
            if orig_el:
                raw_orig = await orig_el.inner_text()
                original_price = self._parse_price(raw_orig)

            # Stock status
            stock_status = "Unknown"
            stock_el = await page.query_selector("[class*='stock-availability']")
            if stock_el:
                stock_text = (await stock_el.inner_text()).lower()
                if "in stock" in stock_text:
                    stock_status = "InStock"
                elif "out of stock" in stock_text:
                    stock_status = "OutOfStock"
                elif "limited" in stock_text or "few" in stock_text:
                    stock_status = "LowStock"

            # Image
            image_url: Optional[str] = None
            img_el = await page.query_selector("img[class*='product-image']")
            if img_el:
                image_url = await img_el.get_attribute("src")

            # External ID from URL
            external_id = self._extract_plid(url)

            if not title or price == 0.0:
                logger.warning(f"[Takealot] Incomplete data for {url}")
                return None

            return ScrapedProduct(
                retailer_id=self.RETAILER_ID,
                title=title,
                price=price,
                original_price=original_price,
                stock_status=stock_status,
                url=url,
                image_url=image_url,
                external_id=external_id,
            )
        finally:
            await page.close()

    async def scrape_category(
        self, category_url: str, max_pages: int = 3
    ) -> list[ScrapedProduct]:
        results: list[ScrapedProduct] = []
        for page_num in range(1, max_pages + 1):
            paginated_url = f"{category_url}?page={page_num}"
            page = await self._fetch(paginated_url, wait_selector="[class*='product-card']")
            try:
                links = await page.query_selector_all("a[class*='product-card']")
                urls = []
                for link in links:
                    href = await link.get_attribute("href")
                    if href:
                        full_url = href if href.startswith("http") else f"{self.BASE_URL}{href}"
                        urls.append(full_url)

                if not urls:
                    logger.info(f"[Takealot] No more products at page {page_num}")
                    break

                logger.info(f"[Takealot] Scraping {len(urls)} products on page {page_num}")
                for url in urls[:10]:  # Limit per page to be polite
                    try:
                        product = await self.scrape_product(url)
                        if product:
                            results.append(product)
                    except Exception as e:
                        logger.error(f"[Takealot] Product error: {e}")
            finally:
                await page.close()

        return results

    @staticmethod
    def _parse_price(text: str) -> float:
        cleaned = re.sub(r"[^\d.]", "", text.replace(",", ""))
        try:
            return float(cleaned)
        except ValueError:
            return 0.0

    @staticmethod
    def _extract_plid(url: str) -> Optional[str]:
        match = re.search(r"PLID(\d+)", url)
        return match.group(0) if match else None