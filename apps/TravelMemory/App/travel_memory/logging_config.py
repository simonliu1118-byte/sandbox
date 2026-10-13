from __future__ import annotations

import logging
import sys
from logging.handlers import RotatingFileHandler
from pathlib import Path

from .paths import AppPaths


class MaxLevelFilter(logging.Filter):
    def __init__(self, maximum: int) -> None:
        super().__init__()
        self.maximum = maximum

    def filter(self, record: logging.LogRecord) -> bool:
        return record.levelno <= self.maximum


def configure_logging(paths: AppPaths) -> logging.Logger:
    paths.ensure()
    logger = logging.getLogger("TravelMemory")
    logger.setLevel(logging.DEBUG)
    logger.handlers.clear()
    formatter = logging.Formatter(
        "%(asctime)s.%(msecs)03d [%(levelname)s] %(name)s %(message)s",
        "%Y-%m-%d %H:%M:%S",
    )

    normal = RotatingFileHandler(
        paths.log_file, maxBytes=8 * 1024 * 1024, backupCount=3, encoding="utf-8"
    )
    normal.setLevel(logging.DEBUG)
    normal.addFilter(MaxLevelFilter(logging.WARNING))
    normal.setFormatter(formatter)
    logger.addHandler(normal)

    errors = RotatingFileHandler(
        paths.error_log_file, maxBytes=4 * 1024 * 1024, backupCount=3, encoding="utf-8"
    )
    errors.setLevel(logging.ERROR)
    errors.setFormatter(formatter)
    logger.addHandler(errors)

    if not getattr(sys, "frozen", False):
        console = logging.StreamHandler()
        console.setLevel(logging.INFO)
        console.setFormatter(formatter)
        logger.addHandler(console)
    return logger


def write_crash_report(paths: AppPaths, exc_type: type[BaseException], exc: BaseException, tb: object) -> Path:
    import datetime as dt
    import platform
    import traceback

    paths.ensure()
    stamp = dt.datetime.now().strftime("%Y%m%d_%H%M%S")
    report = paths.diagnostics / f"TravelMemory_Crash_{stamp}.txt"
    text = [
        "旅行記憶 Crash Report",
        "=" * 72,
        f"Time: {dt.datetime.now().isoformat(timespec='seconds')}",
        f"Python: {platform.python_version()}",
        f"Platform: {platform.platform()}",
        "",
        "".join(traceback.format_exception(exc_type, exc, tb)),
    ]
    report.write_text("\n".join(text), encoding="utf-8")
    return report
