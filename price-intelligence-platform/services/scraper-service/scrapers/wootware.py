from __future__ import annotations

import re
from typing import Optional

from loguru import logger

from models.product import ScrapedProduct
from scrapers.base import BaseScraper


class WootwareScraper(BaseScraper):
    RETAILER_ID = 3
    RETAILER_NAME = "Wootware"
    BASE_URL = "https://www.wootware.co.za"

    async def scrape_product(self, url: str) -> Optional[ScrapedProduct]:
        page = await self._fetch(url, wait_selector=".product-name")
        try:
            # Title
            title_el = await page.query_selector(".product-name h1")
            title = (await title_el.inner_text()).strip() if title_el else ""

            # Price
            price = 0.0
            price_el = await page.query_selector(".price-box .price")
            if price_el:
                price = self._parse_price(await price_el.inner_text())

            # Original price
            original_price: Optional[float] = None
            old_price_el = await page.query_selector(".price-box .old-price .price")
            if old_price_el:
                original_price = self._parse_price(await old_price_el.inner_text())

            # Stock
            stock_status = "Unknown"
            stock_el = await page.query_selector(".availability")
            if stock_el:
                stock_text = (await stock_el.inner_text()).lower()
                stock_status = (
                    "InStock" if "in stock" in stock_text
                    else "OutOfStock" if "out of stock" in stock_text
                    else "Unknown"
                )

            # Image
            image_url: Optional[str] = None
            img_el = await page.query_selector(".product-image-gallery img")
            if img_el:
                image_url = await img_el.get_attribute("src")

            if not title or price == 0.0:
                return None

            return ScrapedProduct(
                retailer_id=self.RETAILER_ID,
                title=title,
                price=price,
                original_price=original_price,
                stock_status=stock_status,
                url=url,
                image_url=image_url,
            )
        finally:
            await page.close()

    async def scrape_category(
        self, category_url: str, max_pages: int = 3
    ) -> list[ScrapedProduct]:
        results: list[ScrapedProduct] = []
        for page_num in range(1, max_pages + 1):
            paginated = f"{category_url}?p={page_num}"
            page = await self._fetch(paginated, wait_selector=".product-item")
            try:
                links = await page.query_selector_all(".product-item-info a.product-item-link")
                urls = [await l.get_attribute("href") for l in links]
                urls = [u for u in urls if u]
                if not urls:
                    break
                for url in urls[:10]:
                    try:
                        product = await self.scrape_product(url)
                        if product:
                            results.append(product)
                    except Exception as e:
                        logger.error(f"[Wootware] {e}")
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