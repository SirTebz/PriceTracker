import httpx
from loguru import logger
from models.product import ScrapedProduct


class ApiClient:
    """Sends scraped products to the PriceIQ API ingest endpoint."""

    def __init__(self, base_url: str, internal_key: str) -> None:
        self.base_url = base_url.rstrip("/")
        self.headers = {
            "X-Internal-Key": internal_key,
            "Content-Type": "application/json",
        }

    async def ingest_product(self, product: ScrapedProduct) -> bool:
        url = f"{self.base_url}/api/system/ingest"
        try:
            async with httpx.AsyncClient(timeout=15.0) as client:
                response = await client.post(
                    url,
                    json=product.to_api_payload(),
                    headers=self.headers,
                )
                if response.status_code in (200, 201):
                    logger.debug(f"Ingested: {product.title}")
                    return True
                else:
                    logger.warning(
                        f"Ingest failed [{response.status_code}]: {product.title} — {response.text}"
                    )
                    return False
        except Exception as e:
            logger.error(f"Ingest error for {product.url}: {e}")
            return False

    async def ingest_batch(self, products: list[ScrapedProduct]) -> dict:
        success, failed = 0, 0
        for product in products:
            if await self.ingest_product(product):
                success += 1
            else:
                failed += 1
        logger.info(f"Batch complete — ✓ {success} ingested, ✗ {failed} failed")
        return {"success": success, "failed": failed}