from __future__ import annotations

import logging
from datetime import datetime, timezone
from functools import lru_cache
from zoneinfo import ZoneInfo, ZoneInfoNotFoundError


class TimeZoneService:
    """離線經緯度時區判定。優先使用 timezonefinder，缺少套件時使用有限備援。"""

    def __init__(self, logger: logging.Logger | None = None) -> None:
        self.logger = logger or logging.getLogger("TravelMemory")
        self._finder = None
        try:
            from timezonefinder import TimezoneFinder  # type: ignore

            self._finder = TimezoneFinder(in_memory=True)
        except Exception as exc:  # pragma: no cover - depends on optional runtime
            self.logger.warning("timezonefinder unavailable; using regional fallback: %s", exc)

    @lru_cache(maxsize=100_000)
    def detect(self, latitude_rounded: float, longitude_rounded: float) -> str:
        latitude = float(latitude_rounded)
        longitude = float(longitude_rounded)
        if self._finder is not None:
            try:
                result = self._finder.timezone_at(lat=latitude, lng=longitude)
                if not result:
                    result = self._finder.closest_timezone_at(lat=latitude, lng=longitude)
                if result:
                    return str(result)
            except Exception as exc:
                self.logger.debug("Timezone lookup failed at %s,%s: %s", latitude, longitude, exc)
        return self._fallback(latitude, longitude)

    def detect_for_point(self, latitude: float, longitude: float) -> str:
        return self.detect(round(latitude, 4), round(longitude, 4))

    @staticmethod
    def to_local(utc_value: datetime, timezone_id: str) -> datetime:
        if utc_value.tzinfo is None:
            utc_value = utc_value.replace(tzinfo=timezone.utc)
        try:
            return utc_value.astimezone(ZoneInfo(timezone_id)).replace(tzinfo=None)
        except ZoneInfoNotFoundError:
            return utc_value.astimezone(timezone.utc).replace(tzinfo=None)

    @staticmethod
    def _fallback(latitude: float, longitude: float) -> str:
        # 本區域備援僅在 timezonefinder 未安裝時使用；正式封裝會包含 timezonefinder/tzdata。
        if 20 <= latitude <= 46 and 122 <= longitude <= 154:
            return "Asia/Tokyo"
        if 21 <= latitude <= 26.5 and 119 <= longitude <= 123:
            return "Asia/Taipei"
        if 18 <= latitude <= 54 and 73 <= longitude <= 135:
            return "Asia/Shanghai"
        if 33 <= latitude <= 39 and 124 <= longitude <= 132:
            return "Asia/Seoul"
        if 24 <= latitude <= 50 and -126 <= longitude <= -66:
            return "America/New_York" if longitude > -100 else "America/Los_Angeles"
        if 35 <= latitude <= 72 and -12 <= longitude <= 45:
            return "Europe/Paris"
        return "Etc/UTC"
