from typing import Optional
import httpx
from loguru import logger
from models.schemas import CanonicalProduct, MatchResult


class ApiClient:
    """Communicates with the PriceIQ API."""

    def __init__(self, base_url: str, internal_key: str) -> None:
        self.base_url = base_url.rstrip("/")
        self.headers = {
            "X-Internal-Key": internal_key,
            "Content-Type": "application/json",
        }

    async def get_all_products(self) -> list[CanonicalProduct]:
        """Fetch all canonical products for matching."""
        url = f"{self.base_url}/api/products/search?pageSize=500"
        try:
            async with httpx.AsyncClient(timeout=30.0) as client:
                response = await client.get(url, headers=self.headers)
                if response.status_code == 200:
                    data = response.json()
                    items = data.get("data", {}).get("items", [])
                    return [
                        CanonicalProduct(
                            id=p["id"],
                            name=p["name"],
                            brand=p.get("brand"),
                            category=p.get("category"),
                            description=p.get("description"),
                        )
                        for p in items
                    ]
                logger.warning(f"Failed to fetch products: {response.status_code}")
                return []
        except Exception as e:
            logger.error(f"API error fetching products: {e}")
            return []

    async def submit_match(self, result: MatchResult) -> bool:
        """Post a match result back to the API."""
        url = f"{self.base_url}/api/system/match"
        try:
            async with httpx.AsyncClient(timeout=15.0) as client:
                response = await client.post(
                    url,
                    json={
                        "listingId": result.listing_id,
                        "matchedProductId": result.matched_product_id,
                        "similarityScore": result.similarity_score,
                        "matchMethod": result.match_method,
                    },
                    headers=self.headers,
                )
                if response.status_code in (200, 201):
                    logger.debug(
                        f"Match submitted: listing {result.listing_id} "
                        f"→ product {result.matched_product_id} "
                        f"({result.similarity_score:.2%})"
                    )
                    return True
                logger.warning(f"Match submit failed [{response.status_code}]")
                return False
        except Exception as e:
            logger.error(f"Match submit error: {e}")
            return False

    async def create_product(self, name: str, brand: Optional[str],
                              category: Optional[str]) -> Optional[str]:
        """Create a new canonical product when no match is found."""
        url = f"{self.base_url}/api/products"
        try:
            async with httpx.AsyncClient(timeout=15.0) as client:
                response = await client.post(
                    url,
                    json={"name": name, "brand": brand, "category": category},
                    headers=self.headers,
                )
                if response.status_code in (200, 201):
                    data = response.json()
                    product_id = data.get("data", {}).get("id")
                    logger.info(f"New canonical product created: {name} ({product_id})")
                    return product_id
                logger.warning(f"Product create failed [{response.status_code}]")
                return None
        except Exception as e:
            logger.error(f"Product create error: {e}")
            return None