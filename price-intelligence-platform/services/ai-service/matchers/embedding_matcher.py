"""
Core AI matching engine using sentence-transformers embeddings
combined with string similarity for high accuracy matching.
"""
from __future__ import annotations

import numpy as np
from sentence_transformers import SentenceTransformer
from sklearn.metrics.pairwise import cosine_similarity
from loguru import logger
from typing import Optional

from models.schemas import CanonicalProduct, ListingPayload, MatchResult
from matchers.text_utils import build_product_text, extract_brand, extract_model_number


class EmbeddingMatcher:
    """
    Matches scraped listings to canonical products using:
    1. Sentence embedding cosine similarity (primary)
    2. Brand/model exact match boost (secondary)
    3. Threshold gating (reject weak matches → create new product)
    """

    def __init__(self, model_name: str = "all-MiniLM-L6-v2",
                 threshold: float = 0.80) -> None:
        logger.info(f"Loading embedding model: {model_name}")
        self.model = SentenceTransformer(model_name)
        self.threshold = threshold
        self._product_cache: list[CanonicalProduct] = []
        self._embeddings: Optional[np.ndarray] = None
        logger.info("Embedding model loaded.")

    def index_products(self, products: list[CanonicalProduct]) -> None:
        """
        Build an embedding index from all canonical products.
        Call this before running any matches.
        """
        if not products:
            logger.warning("No products to index.")
            self._product_cache = []
            self._embeddings = None
            return

        self._product_cache = products
        texts = [
            build_product_text(p.name, p.brand, p.category, p.description)
            for p in products
        ]
        logger.info(f"Indexing {len(texts)} canonical products...")
        self._embeddings = self.model.encode(texts, convert_to_numpy=True,
                                              show_progress_bar=False)
        logger.info("Product index built.")

    def match(self, listing: ListingPayload) -> MatchResult:
        """
        Find the best matching canonical product for a listing.
        Returns a MatchResult — check is_new_product to know if a new
        canonical product should be created.
        """
        if self._embeddings is None or len(self._product_cache) == 0:
            logger.warning("Product index is empty — marking as new product.")
            return MatchResult(
                listing_id=listing.listing_id,
                matched_product_id=listing.product_id,
                similarity_score=0.0,
                is_new_product=True,
            )

        # Encode the listing
        listing_text = build_product_text(
            listing.title, listing.brand, listing.category
        )
        listing_embedding = self.model.encode(
            [listing_text], convert_to_numpy=True, show_progress_bar=False
        )

        # Cosine similarity against all indexed products
        similarities = cosine_similarity(listing_embedding, self._embeddings)[0]
        best_idx = int(np.argmax(similarities))
        best_score = float(similarities[best_idx])

        # Apply brand/model boost
        best_score = self._apply_boost(listing, self._product_cache[best_idx], best_score)

        logger.debug(
            f"Best match: '{self._product_cache[best_idx].name}' "
            f"score={best_score:.3f} (threshold={self.threshold})"
        )

        if best_score >= self.threshold:
            return MatchResult(
                listing_id=listing.listing_id,
                matched_product_id=self._product_cache[best_idx].id,
                similarity_score=round(best_score, 4),
                match_method="AI",
                is_new_product=False,
            )
        else:
            # Score too low — this is a genuinely new product
            logger.info(
                f"No match found for '{listing.title}' "
                f"(best score {best_score:.3f} < {self.threshold}) — new product"
            )
            return MatchResult(
                listing_id=listing.listing_id,
                matched_product_id=listing.product_id,
                similarity_score=round(best_score, 4),
                match_method="AI",
                is_new_product=True,
            )

    def _apply_boost(self, listing: ListingPayload,
                      candidate: CanonicalProduct,
                      base_score: float) -> float:
        """
        Boost score by 0.05 if brand matches exactly.
        Boost score by 0.08 if model number matches exactly.
        Cap at 1.0.
        """
        score = base_score
        listing_brand = (listing.brand or extract_brand(listing.title) or "").lower()
        candidate_brand = (candidate.brand or "").lower()

        if listing_brand and candidate_brand and listing_brand == candidate_brand:
            score += 0.05

        listing_model = extract_model_number(listing.title)
        candidate_model = extract_model_number(candidate.name)

        if listing_model and candidate_model:
            if listing_model.lower() == candidate_model.lower():
                score += 0.08

        return min(score, 1.0)

    def batch_match(self, listings: list[ListingPayload]) -> list[MatchResult]:
        """Match a batch of listings efficiently."""
        results = []
        for listing in listings:
            try:
                result = self.match(listing)
                results.append(result)
            except Exception as e:
                logger.error(f"Match failed for listing {listing.listing_id}: {e}")
        return results