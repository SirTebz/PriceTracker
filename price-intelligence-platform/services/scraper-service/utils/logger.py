import sys
from loguru import logger


def setup_logger(level: str = "INFO") -> None:
    logger.remove()
    logger.add(
        sys.stdout,
        level=level,
        format="<green>{time:YYYY-MM-DD HH:mm:ss}</green> | "
               "<level>{level: <8}</level> | "
               "<cyan>{name}</cyan>:<cyan>{line}</cyan> - "
               "<level>{message}</level>",
        colorize=True,
    )
    logger.add(
        "logs/scraper.log",
        level=level,
        rotation="10 MB",
        retention="7 days",
        compression="zip",
    )