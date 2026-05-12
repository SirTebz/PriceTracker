from pydantic import BaseModel
from typing import Optional


class ListingPayload(BaseModel):
    """A scraped listing waiting to be matched to a canonical product."""
    listing_id: str
    product_id: str          # Current stub product ID (may be replaced)
    title: str
    brand: Optional[str] = None
    category: Optional[str] = None
    price: float
    retailer_id: int
    retailer_name: str
    image_url: Optional[str] = None


class CanonicalProduct(BaseModel):
    """A canonical product from the API."""
    id: str
    name: str
    brand: Optional[str] = None
    category: Optional[str] = None
    description: Optional[str] = None


class MatchResult(BaseModel):
    """Result of AI matching."""
    listing_id: str
    matched_product_id: str
    similarity_score: float
    match_method: str = "AI"
    is_new_product: bool = False