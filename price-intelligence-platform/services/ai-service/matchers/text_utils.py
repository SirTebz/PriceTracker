"""
Utility functions for cleaning and normalising product text
before embedding or similarity comparison.
"""
import re
from typing import Optional


# Common noise words to strip from product titles
_NOISE = re.compile(
    r"\b(the|a|an|and|or|with|for|in|on|of|to|by|from|inc|ltd|co)\b",
    re.IGNORECASE,
)

# Storage sizes — normalise "1TB" → "1000GB" for consistent comparison
_STORAGE = re.compile(r"(\d+)\s*TB", re.IGNORECASE)


def clean_title(title: str) -> str:
    """Remove noise, normalise casing and whitespace."""
    title = _STORAGE.sub(lambda m: f"{int(m.group(1)) * 1000}GB", title)
    title = _NOISE.sub(" ", title)
    title = re.sub(r"[^\w\s]", " ", title)   # strip punctuation
    title = re.sub(r"\s+", " ", title)        # collapse spaces
    return title.strip().lower()


def build_product_text(name: str,
                        brand: Optional[str] = None,
                        category: Optional[str] = None,
                        description: Optional[str] = None) -> str:
    """
    Combine product fields into a single string for embedding.
    Brand and category are weighted by repetition.
    """
    parts = []
    if brand:
        parts.append(brand)        # mention brand first (higher weight)
    parts.append(clean_title(name))
    if category:
        parts.append(category)
    if description:
        # Only use first 100 chars of description — avoid noise
        parts.append(description[:100])
    return " ".join(parts)


def extract_brand(title: str) -> Optional[str]:
    """
    Naively extract the first word of a title as a brand guess.
    Works well for 'Samsung 65" TV', 'Apple MacBook', etc.
    """
    words = title.strip().split()
    return words[0] if words else None


def extract_model_number(title: str) -> Optional[str]:
    """Extract model numbers like 'WH-1000XM5', 'RTX 4090', 'MX Master 3S'."""
    pattern = re.compile(r"\b([A-Z]{1,4}[-\s]?\d{3,6}[A-Z0-9]*)\b", re.IGNORECASE)
    match = pattern.search(title)
    return match.group(0) if match else None