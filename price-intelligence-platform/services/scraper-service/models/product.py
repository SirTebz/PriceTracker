from pydantic import BaseModel
from typing import Optional
from datetime import datetime


class ScrapedProduct(BaseModel):
    retailer_id: int
    title: str
    price: float
    original_price: Optional[float] = None
    stock_status: str = "Unknown"   # InStock | OutOfStock | LowStock | Unknown
    url: str
    image_url: Optional[str] = None
    external_id: Optional[str] = None
    scraped_at: datetime = datetime.utcnow()

    def to_api_payload(self) -> dict:
        return {
            "retailerId": self.retailer_id,
            "title": self.title,
            "price": self.price,
            "originalPrice": self.original_price,
            "stockStatus": self.stock_status,
            "productUrl": self.url,
            "imageUrl": self.image_url,
            "externalId": self.external_id,
        }